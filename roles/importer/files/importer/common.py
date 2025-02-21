import traceback
import sys, time
import json, requests, requests.packages
from socket import gethostname
from typing import List
import importlib.util
from fwo_const import importer_base_dir
from pathlib import Path
sys.path.append(importer_base_dir) # adding absolute path here once
import fwo_api
from fwo_log import getFwoLogger
from fwo_const import fw_module_name
from fwo_const import import_tmp_path
import fwo_globals
from fwo_exception import FwLoginFailed, ImportRecursionLimitReached
from fwo_base import stringIsUri, ConfigAction, ConfFormat
import fwo_file_import
from fwoBaseImport import ImportState
from models.fwconfig_normalized import FwConfig, FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from models.gateway import Gateway
from fwconfig_base import calcManagerUidHash
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.gateway_controller import GatewayController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.rollback import FwConfigImportRollback

"""  
    import_management: import a single management (if no import for it is running)
    lock mgmt for import via FWORCH API call, generating new import_id y
    check if we need to import (no md5, api call if anything has changed since last import)
    get complete config (get, enrich, parse)
    write into json dict write json dict to new table (single entry for complete config)
    trigger import from json into csv and from there into destination tables
    release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
    no changes: remove import_control?
"""
def import_management(mgmId=None, ssl_verification=None, debug_level_in=0, 
        limit=150, force=False, clearManagementData=False, suppress_cert_warnings_in=None,
        in_file=None, version=8):

    logger = getFwoLogger()
    config_changed_since_last_import = True
    time_get_config = 0

    importState = ImportState.initializeImport(mgmId, debugLevel=debug_level_in, 
                                               force=force, version=version, 
                                               isClearingImport=clearManagementData, isFullImport=False)

    if not clearManagementData and importState.DataRetentionDays<importState.DaysSinceLastFullImport:
        # run clear import; this makes sure the following import is a full one
        import_management(mgmId=mgmId, ssl_verification=ssl_verification, debug_level_in=debug_level_in, 
            limit=limit, force=True, clearManagementData=True, suppress_cert_warnings_in=suppress_cert_warnings_in,
            in_file=in_file, version=version)
        importState.IsFullImport = True # the now following import is a full one

    if importState.MgmDetails.ImportDisabled and not importState.ForceImport:
        logger.info(f"import_management - import disabled for mgm  {str(mgmId)} - skipping")
    else:
        Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
        gateways = GatewayController.buildGatewayList(importState.FullMgmDetails)

        # only run if this is the correct import module
        if importState.MgmDetails.ImporterHostname != gethostname() and not importState.ForceImport:
            logger.info("import_management - this host (" + gethostname() + ") is not responsible for importing management " + str(mgmId))
            return ""

        fwo_api.setImportLock(importState)
        logger.info("starting import of management " + importState.MgmDetails.Name + '(' + str(mgmId) + "), import_id=" + str(importState.ImportId))
        configNormalized = {}

        if clearManagementData:
            logger.info('this import run will reset the configuration of this management to "empty"')
            configNormalized = FwConfigManagerListController()
            configNormalized.addManager(
                manager=FwConfigManager(
                    ManagerUid=calcManagerUidHash(importState.FullMgmDetails),
                    ManagerName=importState.MgmDetails.Name,
                    IsGlobal=importState.MgmDetails.IsSuperManager,
                    DependantManagerUids=[],
                    Configs=[]
                ))
            configNormalized.ManagerSet[0].Configs.append(
                FwConfigNormalized(
                    action=ConfigAction.INSERT, 
                    network_objects=[], 
                    service_objects=[], 
                    users=[], 
                    zone_objects=[], 
                    rulebases=[],
                    gateways=[]
            ))
            
            importState.IsClearingImport = True # the now following import is a full one
        else:
            if in_file is not None or stringIsUri(importState.MgmDetails.Hostname):
                ### geting config from file ######################
                config_changed_since_last_import, configNormalized = \
                    importFromFile(importState, in_file, gateways)
            else:
                ### getting config from firewall manager API ######
                config_changed_since_last_import, configNormalized = get_config_from_api(importState, {})

                # also import sub managers if they exist
                for subManagerId in importState.MgmDetails.SubManager:
                    subMgrImportState = ImportState.initializeImport(subManagerId, debugLevel=debug_level_in, 
                                            force=force, version=version, 
                                            isClearingImport=clearManagementData, isFullImport=False)
                    config_changed_since_last_import, configNormalizedSub = get_config_from_api(subMgrImportState, {})
                    configNormalized.mergeConfigs(configNormalizedSub)
                    # TODO: destroy configNormalizedSub?

                if importState.ImportVersion>8:
                    configNormalized.ConfigFormat = ConfFormat.NORMALIZED

            time_get_config = int(time.time()) - importState.StartTime
            logger.debug("import_management - getting config total duration " + str(int(time.time()) - importState.StartTime) + "s")

        if config_changed_since_last_import or importState.ForceImport:
            try: # now we import the config via API chunk by chunk:
                # for config_chunk in split_config(importState, configNormalized):
                configNormalized.storeFullNormalizedConfigToFile(importState) # write full config to file (for debugging)

                for managerSet in configNormalized.ManagerSet:
                    for config in managerSet.Configs:
                        # if len(config)>0:
                            if importState.ImportVersion>8:
                                configImporter = FwConfigImport(importState, config)
                                configChecker = FwConfigImportCheckConsistency(configImporter)
                                if len(configChecker.checkConfigConsistency())==0:
                                    try:
                                        configImporter.importConfig()
                                    except:
                                        logger.error("importConfig - unspecified error: " + str(traceback.format_exc()))
                                        importState.increaseErrorCounterByOne()
                                    if importState.ErrorCount>0:
                                        importState.increaseErrorCounter(fwo_api.complete_import(importState))
                                        FwConfigImportRollback(configImporter).rollbackCurrentImport()
                                    else:
                                        configImporter.storeConfigToApi() # to file (for debugging) and to database
                            else:
                                configChunk = config.toJsonLegacy(withAction=False)
                                importState.increaseErrorCounter(fwo_api.import_json_config(importState, configChunk))

                            fwo_api.update_hit_counter(importState, config)

                # currently assuming only one chunk
                # initiateImportStart(importState)
            except:
                logger.error("import_management - unspecified error while importing config via FWO API: " + str(traceback.format_exc()))
                raise
            logger.debug(f"full import duration: {str(int(time.time())-time_get_config-importState.StartTime)}s")

            # TODO: move the following error handling to function
            error_from_imp_control = "assuming error"
            try: # checking for errors during stored_procedure db imort in import_control table
                error_from_imp_control = fwo_api.get_error_string_from_imp_control(importState, {"importId": importState.ImportId})
            except:
                logger.error("import_management - unspecified error while getting error string: " + str(traceback.format_exc()))

            if error_from_imp_control != None and error_from_imp_control != [{'import_errors': None}]:
                importState.increaseErrorCounterByOne()
                importState.appendErrorString(str(error_from_imp_control))
            # TODO: if no objects found at all: at least throw a warning

            # try: # get change count from db
            #     # temporarily only count rule changes until change report also includes other changes
            #     # change_count = fwo_api.count_changes_per_import(fwo_config['fwo_api_base_url'], jwt, current_import_id)
            #     # change_count = fwo_api.count_rule_changes_per_import(importState.FwoConfig.FwoApiUri, importState.Jwt, importState.ImportId)
            #     change_count = fwo_api.count_rule_changes_per_import(importState.FwoConfig.FwoApiUri, importState.Jwt, importState.ImportId)
            # except:
            #     logger.error("import_management - unspecified error while getting change count: " + str(traceback.format_exc()))
            #     raise
        
        importState.increaseErrorCounter(fwo_api.complete_import(importState))

    if not clearManagementData and importState.DataRetentionDays<importState.DaysSinceLastFullImport:
        # delete all imports of the current management before the last but one full import
        configImporter.deleteOldImports()
       
    return importState.ErrorCount


# def initiateImportStart(importState):
#     # now start import by adding a dummy config with flag set
#     emptyDummyConfig = FwConfigNormalized.fromJson( {
#         'action': ConfigAction.INSERT,
#         'network_objects': [],
#         'service_objects': [],
#         'users': [],
#         'zone_objects': [], 
#         'policies': [],
#         'routing': [],
#         'interfaces': []
#     })

#     importState.setChangeCounter (
#         importState.ErrorCount + fwo_api.import_json_config(importState, 
#                                     emptyDummyConfig.toJsonLegacy(withAction=False), 
#                                     startImport=True))


def importFromFile(importState: ImportState, fileName: str = None, gateways: List[Gateway] = []):

    config_changed_since_last_import = True
    
    # set file name in importState
    if fileName == '' or fileName is None: 
        # if the host name is an URI, do not connect to an API but simply read the config from this URI
        if stringIsUri(importState.MgmDetails.Hostname):
            importState.setImportFileName(importState.MgmDetails.Hostname)
    else:
        importState.setImportFileName(fileName)

    configFromFile = fwo_file_import.readJsonConfigFromFile(importState)

    if configFromFile.IsLegacy():
        if isinstance(configFromFile, FwConfig):
            if configFromFile.ConfigFormat == 'NORMALIZED_LEGACY':
                configNormalized = FwConfigManagerList(ConfigFormat=configFromFile.ConfigFormat)
                configNormalized.addManager(manager=FwConfigManager(calcManagerUidHash(importState.FullMgmDetails), importState.MgmDetails.Name))
                configNormalized.ManagerSet[0].Configs.append(FwConfigNormalized(ConfigAction.INSERT, 
                                                                                configFromFile.Config['network_objects'],
                                                                                configFromFile.Config['service_objects'],
                                                                                configFromFile.Config['user_objects'],
                                                                                configFromFile.Config['zone_objects'],
                                                                                configFromFile.Config['rules'],
                                                                                gateways
                                                                                ))
            elif configFromFile.ConfigFormat == 'NORMALIZED':
                # ideally just import from json
                configNormalized = FwConfigManagerList.fromJson(configFromFile)
        else: ### just parsing the native config, note: we need to run get_config_from_api here to do this
            if isinstance(configFromFile, FwConfigManagerList):
                for mgr in configFromFile.ManagerSet:
                    for conf in mgr.Configs:
                        # need to decide how to deal with the multiple results of this loop here!
                        config_changed_since_last_import, configNormalized = get_config_from_api(importState, conf)
            else: 
                config_changed_since_last_import, configNormalized = get_config_from_api(importState, configFromFile)
    else:
        configNormalized = configFromFile

    return config_changed_since_last_import, configNormalized


def get_config_from_api(importState, configNative, import_tmp_path=import_tmp_path, limit=150) -> FwConfigManagerList:
    logger = getFwoLogger()
    errors_found = 1

    try: # pick product-specific importer:
        pkg_name = importState.MgmDetails.DeviceTypeName.lower().replace(' ', '') + importState.MgmDetails.DeviceTypeVersion
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    try: # get the config data from the firewall manager's API: 
        # check for changes from product-specific FW API
        config_changed_since_last_import = importState.ImportFileName != None or \
            fw_module.has_config_changed(configNative, importState.FullMgmDetails, force=importState.ForceImport)
        if config_changed_since_last_import:
            logger.info ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(importState.ForceImport))
        else:
            logger.info ( "has_config_changed: no new changes found")

        if config_changed_since_last_import or importState.ForceImport:
            # get config from product-specific FW API
            _, configNormalized = fw_module.get_config(configNative, importState)
        else:
            configNormalized = {}
    except (FwLoginFailed) as e:
        importState.ErrorString += "  login failed: mgm_id=" + str(importState.MgmDetails.Id) + ", mgm_name=" + importState.MgmDetails.Name + ", " + e.message
        importState.ErrorCount += 1
        logger.error(importState.ErrorString)
        fwo_api.delete_import(importState) # deleting trace of not even begun import
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise FwLoginFailed(e.message)
    except ImportRecursionLimitReached as e:
        importState.ErrorString += "  recursion limit reached: mgm_id=" + str(importState.MgmDetails.Id) + ", mgm_name=" + importState.MgmDetails.Name + ", " + e.message
        importState.ErrorCount += 1
        logger.error(importState.ErrorString)
        fwo_api.delete_import(importState.FwoConfig.FwoApiUri, importState.Jwt, importState.ImportId) # deleting trace of not even begun import
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise ImportRecursionLimitReached(e.message)
    except:
        importState.ErrorString += "  import_management - unspecified error while getting config: " + str(traceback.format_exc())
        logger.error(importState.ErrorString)
        importState.ErrorCount += 1
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise

    writeNativeConfigToFile(importState, configNative)

    logger.debug("import_management: get_config completed (including normalization), duration: " + str(int(time.time()) - importState.StartTime) + "s") 

    return config_changed_since_last_import, configNormalized


def writeNativeConfigToFile(importState, configNative):
    if fwo_globals.debug_level>6:
        logger = getFwoLogger()
        debug_start_time = int(time.time())
        try:
                full_native_config_filename = f"{import_tmp_path}/mgm_id_{str(importState.MgmDetails.Id)}_config_native.json"
                with open(full_native_config_filename, "w") as json_data:
                    json_data.write(json.dumps(configNative, indent=2))
        except:
            logger.error(f"import_management - unspecified error while dumping config to json file: {str(traceback.format_exc())}")
            raise

        time_write_debug_json = int(time.time()) - debug_start_time
        logger.debug(f"import_management - writing debug config json files duration {str(time_write_debug_json)}s")
