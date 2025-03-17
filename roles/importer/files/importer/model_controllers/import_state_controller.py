import time
from datetime import datetime
import requests, requests.packages

from fwo_log import getFwoLogger
from fwo_config import readConfig
from fwo_const import fwo_config_filename, importer_user_name
import fwo_api
from fwo_exception import FwoApiLoginFailed
import fwo_globals
from models.import_state import ImportState
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_details_controller import ManagementDetailsController
from model_controllers.import_statistics_controller import ImportStatisticsController

"""Used for storing state during import process per management"""
class ImportStateController(ImportState):

    def __init__(self, debugLevel, configChangedSinceLastImport, fwoConfig, mgmDetails, jwt, force, version=8, isFullImport=False, isClearingImport=False):
        self.Stats = ImportStatisticsController()
        self.StartTime = int(time.time())
        self.DebugLevel = debugLevel
        self.ConfigChangedSinceLastImport = configChangedSinceLastImport
        self.FwoConfig = fwoConfig
        self.MgmDetails = ManagementDetailsController.fromJson(mgmDetails)
        self.FullMgmDetails = mgmDetails
        self.ImportId = None
        self.Jwt = jwt
        self.ImportFileName = None
        self.ForceImport = force
        self.ImportVersion = int(version)
        self.IsFullImport = isFullImport
        self.IsClearingImport = isClearingImport
        super().__init__(fwoConfig.FwoApiUri, jwt)

    def __str__(self):
        return f"{str(self.ManagementDetails)}({self.age})"
    
    def setImportFileName(self, importFileName):
        self.ImportFileName = importFileName

    def setImportId(self, importId):
        self.ImportId = importId

    def setChangeCounter(self, changeNo):
        self.Stats.ChangeCount = changeNo

    def increaseErrorCounter(self, errorNo):
        self.Stats.ErrorCount = self.Stats.ErrorCount + errorNo

    def increaseErrorCounterByOne(self):
        self.increaseErrorCounter(1)

    def appendErrorString(self, errorStr):
        self.Stats.ErrorDetails.append(errorStr)

    @classmethod
    def initializeImport(cls, mgmId, debugLevel=0, suppressCertWarnings=False, 
                         sslVerification=False, force=False, version=8,
                         isClearingImport=False, isFullImport=False
                         ):

        def _check_input_parameters(mgmId):
            if mgmId is None:
                raise BaseException("parameter mgm_id is mandatory")

        logger = getFwoLogger()
        _check_input_parameters(mgmId)

        fwoConfig = FworchConfigController.fromJson(readConfig(fwo_config_filename))

        # authenticate to get JWT
        try:
            jwt = fwo_api.login(importer_user_name, fwoConfig.ImporterPassword, fwoConfig.FwoUserMgmtApiUri)
        except FwoApiLoginFailed as e:
            logger.error(e.message)
            raise
        except:
            raise

        # set global https connection values
        fwo_globals.setGlobalValues (suppress_cert_warnings_in=suppressCertWarnings, verify_certs_in=sslVerification, debug_level_in=debugLevel)
        if fwo_globals.verify_certs is None:    # not defined via parameter
            fwo_globals.verify_certs = fwo_api.get_config_value(fwoConfig.FwoApiUri, jwt, key='importCheckCertificates')=='True'
        if fwo_globals.suppress_cert_warnings is None:    # not defined via parameter
            fwo_globals.suppress_cert_warnings = fwo_api.get_config_value(fwoConfig.FwoApiUri, jwt, key='importSuppressCertificateWarnings')=='True'
        if fwo_globals.suppress_cert_warnings: # not defined via parameter
            requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only    

        try: # get mgm_details (fw-type, port, ip, user credentials):
            mgmDetails = fwo_api.get_mgm_details(fwoConfig.FwoApiUri, jwt, {"mgmId": int(mgmId)}, debugLevel) 
        except:
            logger.error("import_management - error while getting fw management details for mgm=" + str(mgmId) )
            raise

        result = cls (
            debugLevel = int(debugLevel),
            configChangedSinceLastImport = True,
            fwoConfig = fwoConfig,
            mgmDetails = mgmDetails,
            jwt = jwt,
            force = force,
            version = version,
            isClearingImport=isClearingImport,
            isFullImport=isFullImport
        )

        result.setPastImportInfos()
        result.setCoreData()

        if type(result) is str:
            logger.error("error while getting import state")
            raise
        
        return result 

    def setPastImportInfos(self):        
        logger = getFwoLogger()
        
        try: # get past import details (LastFullImport, ...):
            self.DataRetentionDays, self.LastFullImportId, lastFullImportDate = \
                fwo_api.getLastImportDetails(self.FwoConfig.FwoApiUri, self.Jwt, {"mgmId": int(self.MgmDetails.Id)}, self.DebugLevel) 
        except:
            logger.error(f"import_management - error while getting past import details for mgm={str(self.MgmDetails.Id)}")
            raise

        if lastFullImportDate is not None:
            # Convert the string to a datetime object
            pastDate = datetime.strptime(lastFullImportDate, "%Y-%m-%dT%H:%M:%S.%f")
            now = datetime.now()
            difference = now - pastDate
            self.DaysSinceLastFullImport = difference.days
        else:
            self.DaysSinceLastFullImport = 0

    def setCoreData(self):        
        # logger = getFwoLogger()
        self.SetTrackMap()
        self.SetActionMap()
        self.SetRulebaseMap()
        self.SetRuleMap()
        self.SetGatewayMap()
        self.SetLinkTypeMap()

    def SetActionMap(self):
        query = "query getActionMap { stm_action { action_name action_id allowed } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_action')
            return {}
        
        map = {}
        for action in result['data']['stm_action']:
            map.update({action['action_name']: action['action_id']})
        self.Actions = map

    def SetTrackMap(self):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_track')
            return {}
        
        map = {}
        for track in result['data']['stm_track']:
            map.update({track['track_name']: track['track_id']})
        self.Tracks = map

    def SetLinkTypeMap(self):
        query = "query getLinkType { stm_link_type { id name } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_link_type')
            return {}
        
        map = {}
        for track in result['data']['stm_link_type']:
            map.update({track['name']: track['id']})
        self.LinkTypes = map

    # limited to the current mgm_id
    # creats a dict with key = rulebase.name and value = rulebase.id
    def SetRulebaseMap(self):

        # TODO: maps need to be updated directly after data changes
        query = """query getRulebaseMap($mgmId: Int) { rulebase(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { id name uid } }"""
        try:
            result = self.call(query=query, queryVariables= {"mgmId": self.MgmDetails.Id})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting rulebases')
            self.RulebaseMap = {}
            return
        
        map = {}
        for rulebase in result['data']['rulebase']:
            rbid = rulebase['id']
            map.update({rulebase['name']: rbid})
            map.update({rulebase['uid']: rbid})
        self.RulebaseMap = map

    # limited to the current mgm_id
    # creats a dict with key = rule.uid and value = rule.id 
    # should be called sparsely, as there might be a lot of rules for a mgmt
    def SetRuleMap(self):
        query = """query getRuleMap($mgmId: Int) { rule(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { id: rule_id uid: rule_uid } }"""
        try:
            result = self.call(query=query, queryVariables= {"mgmId": self.MgmDetails.Id})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting rules')
            self.RuleMap = {}
            return
        
        map = {}
        for rule in result['data']['rule']:
            map.update({rule['uid']: rule['id']})
        self.RuleMap = map

    # limited to the current mgm_id
    # creats a dict with key = rulebase.name and value = rulebase.id
    def SetGatewayMap(self):
        query = """
            query getGatewayMap($mgmId: Int) {
                device(where: {mgm_id: {_eq: $mgmId}}) {
                    id:dev_id
                    name:dev_name
                    uid: dev_uid
                }
            }
    """
        try:
            result = self.call(query=query, queryVariables= {"mgmId": self.MgmDetails.Id})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting gateways')
            self.GatewayMap = {}
            return
        
        map = {}
        for gw in result['data']['device']:
            map.update({gw['name']: gw['id']})
            map.update({gw['uid']: gw['id']})
        self.GatewayMap = map
    
    def lookupRule(self, ruleUid):
        return self.RuleMap.get(ruleUid, None)

    def lookupAction(self, actionStr):
        return self.Actions.get(actionStr.lower(), None)

    def lookupTrack(self, trackStr):
        return self.Tracks.get(trackStr.lower(), None)

    def lookupRulebaseId(self, rulebaseUid):
        rulebaseId = self.RulebaseMap.get(rulebaseUid, None)
        if rulebaseId is None:
            logger = getFwoLogger()
            logger.error(f"Rulebase {rulebaseUid} not found")
        return rulebaseId

    def lookupLinkType(self, linkUid):
        return self.LinkTypes.get(linkUid, None)

    def lookupGatewayId(self, gwUid):
        return self.GatewayMap.get(gwUid, None)
