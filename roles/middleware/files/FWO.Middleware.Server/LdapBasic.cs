using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Encryption;
using FWO.Logging;
using Novell.Directory.Ldap;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the ldap transactions
	/// </summary>
	public partial class Ldap : LdapConnectionBase
	{
		// The following properties are retrieved from the database api:
		// ldap_server ldap_port ldap_search_user ldap_tls ldap_tenant_level ldap_connection_id ldap_search_user_pwd ldap_searchpath_for_users ldap_searchpath_for_roles    
		private const int timeOutInMs = 3000;

		/// <summary>
		/// Default constructor
		/// </summary>
		public Ldap()
		{ }

		/// <summary>
		/// Constructor from parameter struct
		/// </summary>
		public Ldap(LdapGetUpdateParameters ldapGetUpdateParameters) : base(ldapGetUpdateParameters)
		{ }

		/// <summary>
		/// Builds a connection to the specified Ldap server.
		/// </summary>
		/// <returns>Connection to the specified Ldap server.</returns>
		private async Task<LdapConnection> Connect()
		{
			try
			{
				LdapConnectionOptions ldapOptions = new ();
				if (Tls) ldapOptions.ConfigureRemoteCertificateValidationCallback((object sen, X509Certificate? cer, X509Chain? cha, SslPolicyErrors err) => true); // todo: allow real cert validation     
				LdapConnection connection = new (ldapOptions) { SecureSocketLayer = Tls, ConnectionTimeout = timeOutInMs };
				await connection.ConnectAsync(Address, Port);

				return connection;
			}

			catch (Exception exception)
			{
				Log.WriteDebug($"Could not connect to LDAP server {Address}:{Port}: ", exception.Message);
				throw new LdapConnectionException($"Error while trying to reach LDAP server {Address}:{Port}", exception);
			}
		}

		/// <summary>
		/// try an ldap bind, decrypting pwd before bind; using pwd as is if it cannot be decrypted
		/// false if bind fails
		/// </summary>
		private static async Task<bool> TryBind(LdapConnection connection, string? user, string? password)
		{
			if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
			{
				Log.WriteDebug("TryBind", $"found empty ldap user name or password");
				return false;
			}
			else
			{
				string decryptedPassword = password;
				try
				{
					decryptedPassword = AesEnc.Decrypt(password, AesEnc.GetMainKey());
				}
				catch
				{
					Log.WriteDebug("TryBind", $"Could not decrypt password");
					// assuming we already have an unencrypted password, trying this
				}
				await connection.BindAsync(user, decryptedPassword);
			}
			return connection.Bound;
		}

		/// <summary>
		/// Test a connection to the specified Ldap server.
		/// Throws exception if not successful
		/// </summary>
		public async Task TestConnection()
		{
            using LdapConnection connection = await Connect();
            if (!string.IsNullOrEmpty(SearchUser) && !string.IsNullOrEmpty(SearchUserPwd) && !await TryBind(connection, SearchUser, SearchUserPwd!))
            {
				throw new LdapConnectionException("Binding failed for search user");
            }
            if (!string.IsNullOrEmpty(WriteUser) && !string.IsNullOrEmpty(WriteUserPwd) && !await TryBind(connection, WriteUser, WriteUserPwd!))
            {
                throw new LdapConnectionException("Binding failed for write user");
            }
        }

		private string GetUserSearchFilter(string searchPattern)
		{
			string userFilter;
			string searchFilter;
			if (Type == (int)LdapType.ActiveDirectory)
			{
				userFilter = "(&(objectclass=user)(!(objectclass=computer)))";
				searchFilter = $"(|(cn={searchPattern})(sAMAccountName={searchPattern}))";
			}
			else if (Type == (int)LdapType.OpenLdap)
			{
				userFilter = "(|(objectclass=user)(objectclass=person)(objectclass=inetOrgPerson)(objectclass=organizationalPerson))";
				searchFilter = $"(|(cn={searchPattern})(uid={searchPattern}))";
			}
			else // LdapType.Default
			{
				userFilter = "(&(|(objectclass=user)(objectclass=person)(objectclass=inetOrgPerson)(objectclass=organizationalPerson))(!(objectclass=computer)))";
				searchFilter = $"(|(cn={searchPattern})(uid={searchPattern})(userPrincipalName={searchPattern})(mail={searchPattern}))";
			}
			return (searchPattern == null || searchPattern == "") ? userFilter : $"(&{userFilter}{searchFilter})";
		}

		private string GetGroupSearchFilter(string searchPattern)
		{
			string groupFilter;
			string searchFilter;
			if (Type == (int)LdapType.ActiveDirectory)
			{
				groupFilter = "(objectClass=group)";
				searchFilter = $"(|(cn={searchPattern})(name={searchPattern}))";
			}
			else if (Type == (int)LdapType.OpenLdap)
			{
				groupFilter = "(|(objectclass=group)(objectclass=groupofnames)(objectclass=groupofuniquenames))";
				searchFilter = $"(cn={searchPattern})";
			}
			else // LdapType.Default
			{
				groupFilter = "(|(objectclass=group)(objectclass=groupofnames)(objectclass=groupofuniquenames))";
				searchFilter = $"(|(dc={searchPattern})(o={searchPattern})(ou={searchPattern})(cn={searchPattern})(uid={searchPattern})(mail={searchPattern}))";
			}
			return (searchPattern == null || searchPattern == "") ? groupFilter : $"(&{groupFilter}{searchFilter})";
		}

		/// <summary>
		/// Get the LdapEntry for the given user with option to validate credentials
		/// </summary>
		/// <returns>LdapEntry for the given user if found</returns>
		public async Task<LdapEntry?> GetLdapEntry(UiUser user, bool validateCredentials)
		{
			Log.WriteDebug("User Validation", $"Validating User: \"{user.Name}\" ...");
			try
			{
                using LdapConnection connection = await Connect();
                await TryBind(connection, SearchUser, SearchUserPwd);

                LdapSearchConstraints cons = connection.SearchConstraints;
                cons.ReferralFollowing = true;
                connection.Constraints = cons;

                List<LdapEntry> possibleUserEntries = [];

                // If dn was already provided
                if (!string.IsNullOrEmpty(user.Dn))
                {
                    // Try to read user entry directly
                    LdapEntry? userEntry = await connection.ReadAsync(user.Dn);
                    if (userEntry != null)
                    {
                        possibleUserEntries.Add(userEntry);
                    }
                }
                else // Dn was not provided, search for user name
                {
					await SearchUserName(user.Name, possibleUserEntries, connection);
                }

                // If credentials are not checked return user that was found first
                // It could happen that multiple users with the same name were found (impossible if dn was provided)
                if (!validateCredentials)
                {
                	return possibleUserEntries.Count > 0 ? possibleUserEntries[0] : null;
                }
                // If credentials should be checked
                else
                {
                    // Multiple users with the same name could have been found (impossible if dn was provided)
                    foreach (LdapEntry possibleUserEntry in possibleUserEntries)
                    {
                        // Check credentials - if multiple users were found and the credentials are valid this is most definitely the correct user
                        if (await CredentialsValid(connection, possibleUserEntry.Dn, user.Password))
                        {
                            return possibleUserEntry;
                        }
                    }
                }
            }
			catch (LdapException ldapException)
			{
				Log.WriteInfo("Ldap entry exception", $"Ldap entry search at \"{Address}:{Port}\" lead to exception: {ldapException.Message}");
			}
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception \"{Address}:{Port}\"", "Unexpected error while trying to validate user", exception);
			}

			Log.WriteDebug("Invalid Credentials", $"Invalid login credentials - could not authenticate user \"{user.Name}\" on {Address}:{Port}.");
			return null;
		}

		private async Task SearchUserName(string userName, List<LdapEntry> possibleUserEntries, LdapConnection connection)
		{
	        string[] attrList = ["*", "memberof"];
            string userSearchFilter = GetUserSearchFilter(userName);

            // Search for users in ldap with same name as user to validate
            ILdapSearchResults? searchResults = await connection.SearchAsync(
                UserSearchPath,             // top-level path under which to search for user
                LdapConnection.ScopeSub,    // search all levels beneath
                userSearchFilter,
                attrList,
                typesOnly: false
            );

            while (await searchResults.HasMoreAsync())
            {
                LdapEntry? result = await searchResults.NextAsync();
                possibleUserEntries.Add(result);
            }
		}

		/// <summary>
		/// Get the LdapEntry for the given user by Dn
		/// </summary>
		/// <returns>LdapEntry for the given user if found</returns>
		public async Task<LdapEntry?> GetUserDetailsFromLdap(string distinguishedName)
		{
			try
			{
                using LdapConnection connection = await Connect();
                await TryBind(connection, SearchUser, SearchUserPwd);

                LdapSearchConstraints cons = connection.SearchConstraints;
                cons.ReferralFollowing = true;
                connection.Constraints = cons;

                // Try to read user entry directly
                return await connection.ReadAsync(distinguishedName);
            }
			catch (LdapException ldapException)
			{
				Log.WriteInfo("Ldap entry exception", $"Ldap entry search at \"{Address}:{Port}\" lead to exception: {ldapException.Message}");
			}
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception \"{Address}:{Port}\"", "Unexpected error while trying to validate user", exception);
			}
			return null;
		}

		private async Task<bool> CredentialsValid(LdapConnection connection, string dn, string password)
		{
			try
			{
				Log.WriteDebug("User Validation", $"Trying to validate user with distinguished name: \"{dn}\" ...");

				// Try to authenticate as user with given password
				if (await TryBind(connection, dn, password))
				{
					// Return ldap dn
					Log.WriteDebug("User Validation", $"\"{dn}\" successfully authenticated in {Address}:{Port}.");
					return true;
				}
				else
				{
					// this will probably never be reached as an error is thrown before
					// Incorrect password - do nothing, assume its another user with the same username
					Log.WriteDebug($"User Validation {Address}:{Port}", $"Found user with matching uid but different pwd: \"{dn}\".");
				}
			}
			catch (LdapException exc)
			{
				if (exc.ResultCode == 49)  // 49 = InvalidCredentials
					Log.WriteDebug($"Duplicate user {Address}:{Port}", $"Found user with matching uid but different pwd: \"{dn}\".");
				else
					Log.WriteError($"Ldap exception {Address}:{Port}", $"Unexpected error while trying to validate user \"{dn}\".");
			}
			return false;
		}

		/// <summary>
		/// Get the EmailAddress for the given user
		/// </summary>
		/// <returns>EmailAddress of the given user</returns>
		public static string GetEmail(LdapEntry user)
		{
			return user.GetAttributeSet().ContainsKey("mail") ? user.Get("mail").StringValue : "";
		}

		/// <summary>
		/// Get the first name for the given user
		/// </summary>
		/// <returns>first name of the given user</returns>
		public static string GetFirstName(LdapEntry user)
		{
			return user.GetAttributeSet().ContainsKey("givenName") ? user.Get("givenName").StringValue : "";
		}
		
		/// <summary>
		/// Get the last name for the given user
		/// </summary>
		/// <returns>last name of the given user</returns>
		public static string GetLastName(LdapEntry user)
		{
			return user.GetAttributeSet().ContainsKey("sn") ? user.Get("sn").StringValue : "";
		}

		/// <summary>
		/// Get the user name for the given user
		/// </summary>
		/// <returns>username of the given user</returns>
		public static string GetName(LdapEntry user)
		{
			// active directory:
			if (user.GetAttributeSet().ContainsKey("sAMAccountName"))
			{
				return user.Get("sAMAccountName").StringValue;
			}

			// openldap:
			if (user.GetAttributeSet().ContainsKey("uid"))
			{
				return user.Get("uid").StringValue;
			}
			return "";
		}
		
		/// <summary>
		/// Get the tenant name for the given user
		/// </summary>
		/// <returns>tenant name of the given user</returns>
		public string GetTenantName(LdapEntry user)
		{
			DistName dn = new (user.Dn);
			return dn.GetTenantNameViaLdapTenantLevel (TenantLevel);
		}
		
		/// <summary>
		/// Get the groups for the given user
		/// </summary>
		/// <returns>list of groups for the given user</returns>
		public List<string> GetGroups(LdapEntry user)
		{
			// Simplest way as most ldap types should provide the memberof attribute.
			// - Probably this doesn't work for nested groups.
			// - Some systems may only save the "primaryGroupID", then we would have to resolve the name.
			// - Some others may force us to look into all groups to find the membership.
			List<string> groups = [];
			foreach (var attribute in user.GetAttributeSet())
			{
				if (attribute.Name.ToLower() == "memberof")
				{
					foreach (string membership in attribute.StringValueArray)
					{
						if (GroupSearchPath != null && membership.EndsWith(GroupSearchPath))
						{
							groups.Add(membership);
						}
					}
				}
			}
			return groups;
		}

		/// <summary>
		/// Change the password of the given user
		/// </summary>
		/// <returns>error message if not successful</returns>
		public async Task<string> ChangePassword(string userDn, string oldPassword, string newPassword)
		{
			try
			{
                using LdapConnection connection = await Connect();
                // Try to authenticate as user with old password
                if (await TryBind(connection, userDn, oldPassword))
                {
                    // authentication was successful (user is bound): set new password
                    LdapAttribute attribute = new("userPassword", newPassword);
                    LdapModification[] mods = [new LdapModification(LdapModification.Replace, attribute)];

                    await connection.ModifyAsync(userDn, mods);
                    Log.WriteDebug("Change password", $"Password for user {userDn} changed in {Address}:{Port}");
                }
                else
                {
                    return "wrong old password";
                }
            }
			catch (Exception exception)
			{
				return exception.Message;
			}
			return "";
		}

		/// <summary>
		/// Set the password of the given user
		/// </summary>
		/// <returns>error message if not successful</returns>
		public async Task<string> SetPassword(string userDn, string newPassword)
		{
			try
			{
                using LdapConnection connection = await Connect();
                if (await TryBind(connection, WriteUser, WriteUserPwd))
                {
                    // authentication was successful: set new password
                    LdapAttribute attribute = new ("userPassword", newPassword);
                    LdapModification[] mods = [new LdapModification(LdapModification.Replace, attribute)];

                    await connection.ModifyAsync(userDn, mods);
                    Log.WriteDebug("Change password", $"Password for user {userDn} changed in {Address}:{Port}");
                }
                else
                {
                    return "error in write user authentication";
                }
            }
			catch (Exception exception)
			{
				return exception.Message;
			}
			return "";
		}

		/// <summary>
		/// Search all users with search pattern
		/// </summary>
		/// <returns>list of users</returns>
		public async Task<List<LdapUserGetReturnParameters>> GetAllUsers(string searchPattern)
		{
			Log.WriteDebug("GetAllUsers", $"Looking for users with pattern {searchPattern} in {Address}:{Port}");
			List<LdapUserGetReturnParameters> allUsers = [];

			try
			{
                using LdapConnection connection = await Connect();
                // Authenticate as search user
                await TryBind(connection, SearchUser, SearchUserPwd);

                // Search for Ldap users in given directory          
                int searchScope = LdapConnection.ScopeSub;

                LdapSearchConstraints cons = connection.SearchConstraints;
                cons.ReferralFollowing = true;
                connection.Constraints = cons;

                ILdapSearchResults? searchResults = await connection.SearchAsync(UserSearchPath, searchScope, GetUserSearchFilter(searchPattern), null, false);

                while (await searchResults.HasMoreAsync())
                {
                    LdapEntry? entry = await searchResults.NextAsync();
                    allUsers.Add(new LdapUserGetReturnParameters()
                    {
                        UserDn = entry.Dn,
                        Email = entry.GetAttributeSet().ContainsKey("mail") ? entry.Get("mail").StringValue : null
                        // add first and last name of user
                    });
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to get all users", exception);
			}
			return allUsers;
		}

		/// <summary>
		/// Add new user
		/// </summary>
		/// <returns>true if user added</returns>
		public async Task<bool> AddUser(string userDn, string password, string email)
		{
			Log.WriteInfo("Add User", $"Trying to add User: \"{userDn}\"");
			bool userAdded = false;
			try
			{
                using LdapConnection connection = await Connect();
                // Authenticate as write user
                await TryBind(connection, WriteUser, WriteUserPwd);

				string userName = new DistName(userDn).UserName;
				LdapAttributeSet attributeSet = new ()
				{
					new LdapAttribute("objectclass", "inetOrgPerson"),
					new LdapAttribute("sn", userName),
					new LdapAttribute("cn", userName),
					new LdapAttribute("uid", userName),
					new LdapAttribute("userPassword", password),
					new LdapAttribute("mail", email)
				};

                LdapEntry newEntry = new (userDn, attributeSet);

                try
                {
                    //Add the entry to the directory
                    await connection.AddAsync(newEntry);
                    userAdded = true;
                    Log.WriteDebug("Add user", $"User {userName} added in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Add User", $"couldn't add user to LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to add user", exception);
			}
			return userAdded;
		}

		/// <summary>
		/// Update user
		/// </summary>
		/// <returns>true if user updated</returns>
		public async Task<bool> UpdateUser(string userDn, string email)
		{
			Log.WriteInfo("Update User", $"Trying to update User: \"{userDn}\"");
			bool userUpdated = false;
			try
			{
                using LdapConnection connection = await Connect();
                // Authenticate as write user
                await TryBind(connection, WriteUser, WriteUserPwd);
                LdapAttribute attribute = new ("mail", email);
                LdapModification[] mods = [new (LdapModification.Replace, attribute)];

                try
                {
                    //Add the entry to the directory
                    await connection.ModifyAsync(userDn, mods);
                    userUpdated = true;
                    Log.WriteDebug("Update user", $"User {userDn} updated in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Update User", $"couldn't update user in LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to update user", exception);
			}
			return userUpdated;
		}

		/// <summary>
		/// Delete user
		/// </summary>
		/// <returns>true if user deleted</returns>
		public async Task<bool> DeleteUser(string userDn)
		{
			Log.WriteInfo("Delete User", $"Trying to delete User: \"{userDn}\"");
			bool userDeleted = false;
			try
			{
                using LdapConnection connection = await Connect();
                // Authenticate as write user
                await TryBind(connection, WriteUser, WriteUserPwd);

                try
                {
                    //Delete the entry in the directory
                    await connection.DeleteAsync(userDn);
                    userDeleted = true;
                    Log.WriteDebug("Delete user", $"User {userDn} deleted in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Delete User", $"couldn't delete user in LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to delete user", exception);
			}
			return userDeleted;
		}

		private static bool IsFullyQualifiedDn(string name)
		{
			name = name.ToLower();
			return name.Contains(',') && (name.StartsWith("cn=") || name.StartsWith("uid="));
		}

		/// <summary>
		/// Add user to entry
		/// </summary>
		/// <returns>true if user added</returns>
		public async Task<bool> AddUserToEntry(string userDn, string entry)
		{
			Log.WriteInfo("Add User to Entry", $"Trying to add User: \"{userDn}\" to Entry: \"{entry}\"");
			return await ModifyUserInEntry(userDn, entry, LdapModification.Add);
		}

		/// <summary>
		/// Remove user from entry
		/// </summary>
		/// <returns>true if user removed</returns>
		public async Task<bool> RemoveUserFromEntry(string userDn, string entry)
		{
			Log.WriteInfo("Remove User from Entry", $"Trying to remove User: \"{userDn}\" from Entry: \"{entry}\"");
			return await ModifyUserInEntry(userDn, entry, LdapModification.Delete);
		}

		/// <summary>
		/// Remove user from all entries
		/// </summary>
		/// <returns>true if user removed from all entries</returns>
		public async Task<bool> RemoveUserFromAllEntries(string userDn)
		{
			List<string> dnList = [userDn]; // group memberships do not need to be regarded here
			List<string> roles = await GetRoles(dnList);
			bool allRemoved = true;
			foreach (var role in roles)
			{
				allRemoved &= await RemoveUserFromEntry(userDn, $"cn={role},{RoleSearchPath}");
			}
			List<string> groups = await GetGroups(dnList);
			foreach (var group in groups)
			{
				allRemoved &= await RemoveUserFromEntry(userDn, $"cn={group},{GroupWritePath}");
			}
			return allRemoved;
		}

		private async Task<bool> ModifyUserInEntry(string userDn, string entry, int ldapModification)
		{
			bool userModified = false;
			try
			{
                using LdapConnection connection = await Connect();
                // Authenticate as write user
                await TryBind(connection, WriteUser, WriteUserPwd);

                // Add a new value to the description attribute
                LdapAttribute attribute = new("uniquemember", userDn);
                LdapModification[] mods = [new(ldapModification, attribute)];

                try
                {
                    //Modify the entry in the directory
                    await connection.ModifyAsync(entry, mods);
                    userModified = true;
                    Log.WriteDebug("Modify Entry", $"Entry {entry} modified in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Modify Entry", $"maybe entry doesn't exist in this LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to modify user", exception);
			}
			return userModified;
		}
    }
}
