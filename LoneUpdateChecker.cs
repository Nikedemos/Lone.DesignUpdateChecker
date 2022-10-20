using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Lone.Design Update Checker", "Nikedemos / DezLife / nivex", "1.3.4")]
    [Description("Checks for available updates of Lone.Design plugins")]
    public class LoneUpdateChecker : RustPlugin
    {
        #region CONST/STATIC
        public const string API_URL = "https://api.lone.design/plugins.json";

        public const string DISCORD_MSG_UPDATE = "{0}/messages/{1}";
        public const string DISCORD_MSG_CREATE = "{0}?wait=true";

        public const string CMD_BLACKLIST = "lone.blacklist";
        public const string PERM_BLACKLIST = "loneupdatechecker.blacklist";

        public static LoneUpdateChecker Instance;

        public static Hash<string, VersionNumber> CurrentPluginVersions;

        public static StringBuilder StringBuilderInstance;
        public static ApiResponse RecentApiResponse;
        public static JsonSerializerSettings ErrorHandling;

        public static VersionNumber InvalidVersionNumber;

        public static Timer RequestTimer;
        private StoredData storedData;
        #endregion

        #region LANG
        public const string MSG_UPDATE_CHECK_FREQUENCY = nameof(MSG_UPDATE_CHECK_FREQUENCY);
        public const string MSG_CONFIG_JUST_UPDATED = nameof(MSG_CONFIG_JUST_UPDATED);
        public const string MSG_CONFIG_LOADING = nameof(MSG_CONFIG_LOADING);
        public const string MSG_CONFIG_ERROR_FAILED_LOADING = nameof(MSG_CONFIG_ERROR_FAILED_LOADING);
        public const string MSG_CONFIG_ERROR_NULL_CONFIG = nameof(MSG_CONFIG_ERROR_NULL_CONFIG);
        public const string MSG_UPDATE_CHECKING_ALL = nameof(MSG_UPDATE_CHECKING_ALL);
        public const string MSG_UPDATE_CHECKING_PLUGIN = nameof(MSG_UPDATE_CHECKING_PLUGIN);
        public const string MSG_PLUGIN_RESPONSE_ERROR = nameof(MSG_PLUGIN_RESPONSE_ERROR);
        public const string MSG_PLUGIN_DESERIALIZATION_ERROR = nameof(MSG_PLUGIN_DESERIALIZATION_ERROR);
        public const string MSG_NO_LONE_PLUGINS_INSTALLED = nameof(MSG_NO_LONE_PLUGINS_INSTALLED);
        public const string MSG_PLUGIN_NOT_IN_LONE_DB = nameof(MSG_PLUGIN_NOT_IN_LONE_DB);
        public const string MSG_PLUGIN_REPONSE_INVALID_VERSION = nameof(MSG_PLUGIN_REPONSE_INVALID_VERSION);
        public const string MSG_PLUGIN_RESPONSE_OUTDATED_VERSION = nameof(MSG_PLUGIN_RESPONSE_OUTDATED_VERSION);
        public const string MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_SINGLE = nameof(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_SINGLE);
        public const string MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_BULK = nameof(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_BULK);

        public const string MSG_PLUGIN_RESPONSE_ALL_PLUGINS_UP_TO_DATE = nameof(MSG_PLUGIN_RESPONSE_ALL_PLUGINS_UP_TO_DATE);
        public const string MSG_PLUGIN_RESPONSE_SINGLE_PLUGIN_UP_TO_DATE = nameof(MSG_PLUGIN_RESPONSE_SINGLE_PLUGIN_UP_TO_DATE);

        public const string MSG_CMD_BLACKLIST_USAGE = nameof(MSG_CMD_BLACKLIST_USAGE);
        public const string MSG_CMD_BLACKLIST_LIST_ALL = nameof(MSG_CMD_BLACKLIST_LIST_ALL);
        public const string MSG_CMD_BLACKLIST_WRONG_ARG = nameof(MSG_CMD_BLACKLIST_WRONG_ARG);
        public const string MSG_CMD_BLACKLIST_EMPTY = nameof(MSG_CMD_BLACKLIST_EMPTY);
        public const string MSG_CMD_BLACKLIST_LIST_HEADER = nameof(MSG_CMD_BLACKLIST_LIST_HEADER);
        public const string MSG_CMD_BLACKLIST_CLEAR_FAILSAFE = nameof(MSG_CMD_BLACKLIST_CLEAR_FAILSAFE);
        public const string MSG_CMD_BLACKLIST_CLEAR_DONE = nameof(MSG_CMD_BLACKLIST_CLEAR_DONE);
        public const string MSG_CMD_BLACKLIST_CLEAR_FAILSAFE_NOPE = nameof(MSG_CMD_BLACKLIST_CLEAR_FAILSAFE_NOPE);
        public const string MSG_CMD_BLACKLIST_ADD_REMOVE_PROVIDE_NAME = nameof(MSG_CMD_BLACKLIST_ADD_REMOVE_PROVIDE_NAME);
        public const string MSG_CMD_BLACKLIST_ADD_NO_MATCHES = nameof(MSG_CMD_BLACKLIST_ADD_NO_MATCHES);
        public const string MSG_CMD_BLACKLIST_ADD_ALREADY_CONTAINS = nameof(MSG_CMD_BLACKLIST_ADD_ALREADY_CONTAINS);
        public const string MSG_CMD_BLACKLIST_REMOVE_DOESNT_CONTAIN = nameof(MSG_CMD_BLACKLIST_REMOVE_DOESNT_CONTAIN);
        public const string MSG_CMD_BLACKLIST_ADDED_DONE = nameof(MSG_CMD_BLACKLIST_ADDED_DONE);
        public const string MSG_CMD_BLACKLIST_REMOVED_DONE = nameof(MSG_CMD_BLACKLIST_REMOVED_DONE);
        public const string MSG_CMD_BLACKLIST_CONFIG_SAVED = nameof(MSG_CMD_BLACKLIST_CONFIG_SAVED);

        private static Dictionary<string, string> LangMessages = new Dictionary<string, string>
        {
            [MSG_UPDATE_CHECK_FREQUENCY] = "The updates will be checked every {0} minute(s)",
            [MSG_CONFIG_JUST_UPDATED] = "It looks like you have just updated from {0} to {1}!",
            [MSG_CONFIG_LOADING] = "Loading configuration file...",
            [MSG_CONFIG_ERROR_FAILED_LOADING] = "\nERROR: COULD NOT READ THE CONFIG FILE!\n{0}\n{1}\nRe-generating default config...\n",
            [MSG_CONFIG_ERROR_NULL_CONFIG] = "\nERROR: CONFIG IS NULL!\nRe-generating default config...\n",
            [MSG_UPDATE_CHECKING_ALL] = "Checking for all plugin updates...",
            [MSG_UPDATE_CHECKING_PLUGIN] = "{0} was just loaded in, checking for updates...",
            [MSG_PLUGIN_RESPONSE_ERROR] = "ERROR HANDLING RESPONSE FROM THE API.\nHTTP CODE {0}\nRESPONSE FROM SERVER:\n{1}\n",
            [MSG_PLUGIN_DESERIALIZATION_ERROR] = "ERROR: COULD NOT DESERIALIZE THE RESPONSE FROM THE API:\nHTTP CODE {0}\nRESPONSE FROM SERVER:\n{1}\n\nThe following seems to be the issue:\n{2}\n{3}\n",
            [MSG_NO_LONE_PLUGINS_INSTALLED] = "It doesn't look like you have any Lone.Design plugins installed (or the plugins that you do, have not had their product page metadata set up correctly). Get some top-notch plugins at https://lone.design and/or let the plugin devs know that their Lone.Design plugin is not showing up in the database.",
            [MSG_PLUGIN_NOT_IN_LONE_DB] = "It doesn't look like this plugin exists in the Lone.design database. It might not be a Lone.Design plugin or the plugin dev has not had their product page metadata set up correctly.",
            [MSG_PLUGIN_REPONSE_INVALID_VERSION] = "**{0}**: ERROR! API returned an invalid version number {1}",
            [MSG_PLUGIN_RESPONSE_OUTDATED_VERSION] = "**{0}**: out of date! Installed version {1}, new version {2}!",
            [MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_SINGLE] = "{0} needs to be updated. Installed version {1}, new version {2}",
            [MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_BULK] = "Found updates for at least 1 Lone.Design plugin, check above!",
            [MSG_PLUGIN_RESPONSE_ALL_PLUGINS_UP_TO_DATE] = "All your Lone.Design plugins seem up to date.",
            [MSG_PLUGIN_RESPONSE_SINGLE_PLUGIN_UP_TO_DATE] = "{0} is up to date.",
            [MSG_CMD_BLACKLIST_USAGE] = "USAGE: " + CMD_BLACKLIST + " [add | remove | list | clear]",
            [MSG_CMD_BLACKLIST_LIST_ALL] = "The following plugins are currently blacklisted:",
            [MSG_CMD_BLACKLIST_WRONG_ARG] = "No such option as {0}. ",
            [MSG_CMD_BLACKLIST_EMPTY] = "There's no plugins currently blacklisted.",
            [MSG_CMD_BLACKLIST_LIST_HEADER] = "There's {0} plugins currently blacklisted:",
            [MSG_CMD_BLACKLIST_CLEAR_FAILSAFE] = "FAILSAFE: To confirm you want to clear all {0} plugins from the blacklist, please use: " + CMD_BLACKLIST + " clear {0}",
            [MSG_CMD_BLACKLIST_CLEAR_DONE] = "You have cleared your entire blacklist successfully.",
            [MSG_CMD_BLACKLIST_CLEAR_FAILSAFE_NOPE] = "You didn't provide a valid number to confirm. Try again?",
            [MSG_CMD_BLACKLIST_ADD_REMOVE_PROVIDE_NAME] = "Please provide the full or partial name of the plugin - uppercase/lowercase doesn't matter.",
            [MSG_CMD_BLACKLIST_ADD_NO_MATCHES] = "No plugin currently loaded in that matches \"{0}\". Use \"o.plugins\" to get a full list.",
            [MSG_CMD_BLACKLIST_ADD_ALREADY_CONTAINS] = "The blacklist already contains an entry for \"{0}\"! Use \"" + CMD_BLACKLIST +" list\" to confirm.",
            [MSG_CMD_BLACKLIST_REMOVE_DOESNT_CONTAIN] = "The blacklist doesn't contain an entry for \"{0}\"! Use \"" + CMD_BLACKLIST + " list\" to confirm.",
            [MSG_CMD_BLACKLIST_ADDED_DONE] = "You have added {0} to the blacklist successfully.",
            [MSG_CMD_BLACKLIST_REMOVED_DONE] = "You have removed {0} from the blacklist successfully.",
            [MSG_CMD_BLACKLIST_CONFIG_SAVED] = "Config file saved.",
        };

        private static string MSG(string msg, string userID = null, params object[] args)
        {
            if (args.Length == 0)
            {
                return Instance.lang.GetMessage(msg, Instance, userID);
            }
            else
            {
                return string.Format(Instance.lang.GetMessage(msg, Instance, userID), args);
            }

        }
        #endregion

        #region HOOKS
        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            //register here...
            lang.RegisterMessages(LangMessages, this);
        }
        private void OnServerInitialized()
        {
            //and also re-register here in case the plugin was hot-loaded after the server started
            lang.RegisterMessages(LangMessages, this);
            permission.RegisterPermission(PERM_BLACKLIST, this);

            timer.Once(1F, OnServerInitializedAfterDelay);
        }

        public void OnServerInitializedAfterDelay()
        {
            Instance = this;

            InvalidVersionNumber = new VersionNumber(0, 0, 0);
            StringBuilderInstance = new StringBuilder();
            ErrorHandling = new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } };
            CurrentPluginVersions = new Hash<string, VersionNumber>();
            LoadConfigData();
            ProcessConfigData();

            AddCovalenceCommand(CMD_BLACKLIST, nameof(CommandBlacklist), PERM_BLACKLIST);

            RequestSend();

            if (Configuration.CheckForUpdatesPeriodically)
            {
                var everyMinute = Configuration.HowManyMinutesBetweenPeriodicalUpdates;
                Instance.PrintWarning(MSG(MSG_UPDATE_CHECK_FREQUENCY, null, everyMinute));
                RequestTimer = timer.Repeat(30f, 14400, () => RequestSend());
            }
        }

        private void Unload()
        {
            if (IsObjectNull(Instance))
            {
                return;
            }

            if (!IsObjectNull(RequestTimer))
            {
                RequestTimer.Destroy();
            }

            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

            RequestTimer = null;
            StringBuilderInstance = null;
            RecentApiResponse = null;
            ErrorHandling = null;
            CurrentPluginVersions = null;
            Instance = null;

        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (IsObjectNull(Instance))
            {
                return;
            }

            if (!Configuration.CheckForUpdatesWhenPluginsLoad)
            {
                return;
            }

            if (!Instance.Configuration.Blacklist.IsNullOrEmpty())
            {
                var shortFilename = $"{plugin.Name}.cs";

                if (Instance.Configuration.Blacklist.Contains(shortFilename))
                {
                    return;
                }
            }

            RequestSend($"{plugin.Name}.cs");

        }
        #endregion

        #region CMD
        [Command(CMD_BLACKLIST)]
        private void CommandBlacklist(IPlayer iplayer, string command, string[] args)
        {
            if (Instance == null)
            {
                return;
            }

            var player = iplayer.Object as BasePlayer;

            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, PERM_BLACKLIST))
                {
                    return;
                }
            }

            if (args.Length == 0)
            {
                iplayer.Reply(MSG(MSG_CMD_BLACKLIST_USAGE, iplayer.Id));
                return;
            }

            bool saveConfigData = false;

            switch (args[0])
            {
                default:
                    {
                        iplayer.Reply(MSG(MSG_CMD_BLACKLIST_WRONG_ARG, iplayer.Id, args[0]) + MSG(MSG_CMD_BLACKLIST_USAGE, iplayer.Id, args[0]));
                    }
                    break;
                case "list":
                    {
                        if (Configuration.Blacklist.IsNullOrEmpty())
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_EMPTY, iplayer.Id));
                            break;
                        }

                        StringBuilder buildResponse = new StringBuilder();

                        buildResponse.AppendLine(MSG(MSG_CMD_BLACKLIST_LIST_HEADER, iplayer.Id, Configuration.Blacklist.Count));

                        var sortedAlphabetically = Configuration.Blacklist.OrderBy(e => e).ToArray();

                        for (var i = 0; i < sortedAlphabetically.Length; i++)
                        {
                            buildResponse.Append("- ");
                            buildResponse.AppendLine(sortedAlphabetically[i]);
                        }

                        iplayer.Reply(buildResponse.ToString());
                    }
                    break;
                case "clear":
                    {
                        if (Configuration.Blacklist.IsNullOrEmpty())
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_EMPTY, iplayer.Id));
                            break;
                        }

                        if (args.Length == 1)
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_CLEAR_FAILSAFE, iplayer.Id, Configuration.Blacklist.Count));
                            break;
                        }

                        var currentCount = Configuration.Blacklist.Count;

                        bool isCorrect = true;

                        int tryParserino;

                        if (!int.TryParse(args[1], out tryParserino))
                        {
                            isCorrect = false;
                        }

                        if (isCorrect)
                        {
                            if (tryParserino != currentCount)
                            {
                                isCorrect = false;
                            }
                        }

                        if (!isCorrect)
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_CLEAR_FAILSAFE_NOPE, iplayer.Id));
                            break;
                        }

                        Configuration.Blacklist = new List<string>();

                        saveConfigData = true;

                        iplayer.Reply(MSG(MSG_CMD_BLACKLIST_CLEAR_DONE, iplayer.Id, currentCount) + "\n" + MSG(MSG_CMD_BLACKLIST_CONFIG_SAVED));

                    }
                    break;
                case "add":
                    {
                        if (args.Length == 1)
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_ADD_REMOVE_PROVIDE_NAME, iplayer.Id));
                            break;
                        }

                        var pluginFound = TryFindPluginByPartialName(args[1]);

                        if (pluginFound == null)
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_ADD_NO_MATCHES, iplayer.Id, args[1]));
                            break;
                        }

                        if (Configuration.Blacklist.Contains(pluginFound))
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_ADD_ALREADY_CONTAINS, iplayer.Id, pluginFound));
                            break;
                        }

                        Configuration.Blacklist.Add(pluginFound);
                        saveConfigData = true;

                        iplayer.Reply(MSG(MSG_CMD_BLACKLIST_ADDED_DONE, iplayer.Id, pluginFound) + "\n" + MSG(MSG_CMD_BLACKLIST_CONFIG_SAVED));
                    }
                    break;
                case "remove":
                    {
                        if (args.Length == 1)
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_ADD_REMOVE_PROVIDE_NAME, iplayer.Id));
                            break;
                        }

                        var pluginFound = TryFindPluginByPartialName(args[1]);

                        if (pluginFound == null)
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_ADD_NO_MATCHES, iplayer.Id, args[1]));
                            break;
                        }

                        if (!Configuration.Blacklist.Contains(pluginFound))
                        {
                            iplayer.Reply(MSG(MSG_CMD_BLACKLIST_REMOVE_DOESNT_CONTAIN, iplayer.Id, pluginFound));
                            break;
                        }

                        Configuration.Blacklist.Remove(pluginFound);
                        saveConfigData = true;

                        iplayer.Reply(MSG(MSG_CMD_BLACKLIST_REMOVED_DONE, iplayer.Id, pluginFound) + "\n" + MSG(MSG_CMD_BLACKLIST_CONFIG_SAVED));

                    }
                    break;
            }

            if (saveConfigData)
            {
                SaveConfigData();
            }
        }
        #endregion

        #region CONFIG
        public class ConfigData
        {
            public VersionNumber Version = new VersionNumber(1, 0, 0);
            public bool CheckForUpdatesWhenPluginsLoad = true;
            public bool CheckForUpdatesPeriodically = true;
            public bool EnableSendingNotificationsToDiscord = false;
            public string WebHookForSendingNotificationsToDiscord = "";
            public uint HowManyMinutesBetweenPeriodicalUpdates = 30;
            public List<string> Blacklist = new List<string>();
        }

        private ConfigData Configuration;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default config...");
            Configuration = new ConfigData();
            SaveConfigData();
        }

        private void ProcessConfigData()
        {
            bool needsSave = false;

            if (Version != Configuration.Version)
            {
                Instance.PrintWarning(MSG(MSG_CONFIG_JUST_UPDATED, null, Configuration.Version, Version));
                Configuration.Version = Version;

                needsSave = true;
            }

            if (Configuration.HowManyMinutesBetweenPeriodicalUpdates < 30)
            {
                Configuration.HowManyMinutesBetweenPeriodicalUpdates = 30;
                needsSave = true;
            }
            if (Configuration.EnableSendingNotificationsToDiscord && string.IsNullOrWhiteSpace(Configuration.WebHookForSendingNotificationsToDiscord))
            {
                Configuration.EnableSendingNotificationsToDiscord = false;
                needsSave = true;
            }

            if (needsSave)
            {
                SaveConfigData();
            }
        }

        private void LoadConfigData()
        {
            bool needsSave = false;

            PrintWarning(MSG(MSG_CONFIG_LOADING));
            try
            {
                Configuration = Config.ReadObject<ConfigData>();
            }
            catch (Exception e)
            {
                Instance.PrintError(MSG(MSG_CONFIG_ERROR_FAILED_LOADING, null, e.Message, e.StackTrace));
                Configuration = new ConfigData();
                needsSave = true;
            }

            if (IsObjectNull(Configuration))
            {
                Instance.PrintError(MSG(MSG_CONFIG_ERROR_NULL_CONFIG));
                needsSave = true;
            }

            if (needsSave)
            {
                SaveConfigData();
            }
        }

        private void SaveConfigData()
        {
            Config.WriteObject(Configuration, true);
        }

        #endregion

        #region WEB REQUEST
        public class ApiResponse : List<ApiPluginInfo>
        {

        }

        public struct ApiPluginInfo
        {
            public string name;

            public string filename;

            public string version;
        }

        public static void RequestSend(string requestPlugins = default(string))
        {
            Action<int, string> callback;

            if (requestPlugins == default(string))
            {
                callback = RequestCallbackBulk;
                Instance.PrintWarning(MSG(MSG_UPDATE_CHECKING_ALL));
            }
            else
            {
                callback = RequestCallbackSingle;
                Instance.PrintWarning(MSG(MSG_UPDATE_CHECKING_PLUGIN, null, requestPlugins));
            }

            RecentApiResponse = null;
            PopulatePluginCache(requestPlugins);

            Instance.webrequest.Enqueue(API_URL, string.Empty, callback, Instance, Core.Libraries.RequestMethod.GET, null, 10F);
        }

        public static void RequestCallbackCommon(int code, string response, bool single)
        {
            if (code != 200)
            {
                Instance.PrintError(MSG(MSG_PLUGIN_RESPONSE_ERROR, null, code, response));
                return;
            }

            try
            {
                RecentApiResponse = JsonConvert.DeserializeObject<ApiResponse>(response, ErrorHandling);
            }
            catch (Exception e)
            {
                Instance.PrintError(MSG(MSG_PLUGIN_DESERIALIZATION_ERROR, null, code, response, e.Message, e.StackTrace));
                return;
            }

            if (RecentApiResponse.Count == 0)
            {
                if (!single)
                {
                    Instance.PrintWarning(MSG(MSG_NO_LONE_PLUGINS_INSTALLED));
                }
                else
                {
                    Instance.PrintWarning(MSG(MSG_PLUGIN_NOT_IN_LONE_DB));
                }

                return;
            }

            ApiPluginInfo currentInfo;
            VersionNumber versionPresent;
            VersionNumber versionFromAPI;

            int outdatedPluginsFound = 0;

            StringBuilderInstance.Clear();
            StringBuilderInstance.AppendLine();

            string lastSingle = "";
            string lastVersionPresent = "";
            string lastVersionFromAPI = "";

            for (var i = 0; i < RecentApiResponse.Count; i++)
            {
                currentInfo = RecentApiResponse[i];

                if (!CurrentPluginVersions.ContainsKey(currentInfo.filename))
                {
                    continue;
                }

                versionPresent = CurrentPluginVersions[currentInfo.filename];
                versionFromAPI = VersionNumberFromString(currentInfo.version);

                lastSingle = currentInfo.filename;
                lastVersionPresent = versionPresent.ToString();
                lastVersionFromAPI = versionFromAPI.ToString();

                if (versionFromAPI == InvalidVersionNumber)
                {
                    StringBuilderInstance.AppendLine(MSG(MSG_PLUGIN_REPONSE_INVALID_VERSION, null, currentInfo.filename, currentInfo.version));
                    continue;
                }

                if (versionFromAPI > versionPresent)
                {
                    StringBuilderInstance.AppendLine(MSG(MSG_PLUGIN_RESPONSE_OUTDATED_VERSION, null, currentInfo.filename, versionPresent, versionFromAPI));

                    outdatedPluginsFound++;
                }
            }

            StringBuilderInstance.AppendLine();
            if (outdatedPluginsFound > 0)
            {
                Instance.PrintError(StringBuilderInstance.ToString().Replace("*", string.Empty));
                if (Instance.Configuration.EnableSendingNotificationsToDiscord)
                    SendOrUpdateDiscordMessage(StringBuilderInstance.ToString(), single ? MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_SINGLE, null, lastSingle, lastVersionPresent, lastVersionFromAPI) : MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_BULK));

                Instance.PrintWarning(single ? MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_SINGLE, null, lastSingle, lastVersionPresent, lastVersionFromAPI) : MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_BULK));
            }
            else
            {
                Instance.PrintWarning(single ? MSG(MSG_PLUGIN_RESPONSE_SINGLE_PLUGIN_UP_TO_DATE, null, lastSingle) : MSG(MSG_PLUGIN_RESPONSE_ALL_PLUGINS_UP_TO_DATE));
            }

        }

        public static void RequestCallbackSingle(int code, string response) => RequestCallbackCommon(code, response, true);

        public static void RequestCallbackBulk(int code, string response) => RequestCallbackCommon(code, response, false);

        public static string TryFindPluginByPartialName(string nameQuery)
        {
            Plugin[] loadedPluginsAlphabetically = Interface.Oxide.RootPluginManager.GetPlugins().OrderBy(e => e.Name).ToArray();

            Plugin currentPlugin;

            string shortFilename;

            nameQuery = nameQuery.ToLower();

            for (var i = 0; i < loadedPluginsAlphabetically.Length; i++)
            {
                currentPlugin = loadedPluginsAlphabetically[i];

                if (string.IsNullOrEmpty(currentPlugin.Filename))
                {
                    continue;
                }

                shortFilename = $"{currentPlugin.Name}.cs";

                if (shortFilename.ToLower().Contains(nameQuery))
                {
                    return shortFilename;
                }
            }

            return null;
        }

        public static void PopulatePluginCache(string filterByName = default(string))
        {
            CurrentPluginVersions.Clear();

            Plugin[] iterateOver = Interface.Oxide.RootPluginManager.GetPlugins().ToArray();

            Plugin currentPlugin;

            string shortFilename;

            for (var i = 0; i < iterateOver.Length; i++)
            {
                currentPlugin = iterateOver[i];

                if (string.IsNullOrEmpty(currentPlugin.Filename))
                {
                    continue;
                }

                shortFilename = $"{currentPlugin.Name}.cs";

                if (!Instance.Configuration.Blacklist.IsNullOrEmpty())
                {
                    if (Instance.Configuration.Blacklist.Contains(shortFilename))
                    {
                        continue;
                    }
                }

                if (filterByName != default(string))
                {
                    if (!shortFilename.Contains(filterByName))
                    {
                        continue;
                    }
                }
                CurrentPluginVersions.Add(shortFilename, currentPlugin.Version);
            }
        }
        #endregion

        #region DISCORD

        public static void SendOrUpdateDiscordMessage(string message, string title)
        {
            string embeds = GenerateEmbeds(message, title);
            StringBuilderInstance.Clear();
            bool data = Instance.storedData.DoesItExistMessageId();
            if (data)
            {
                Instance.webrequest.Enqueue(StringBuilderInstance.AppendFormat(DISCORD_MSG_CREATE, Instance.Configuration.WebHookForSendingNotificationsToDiscord).ToString(), embeds
                    , (code, response) =>
                    {

                        if (code == 404)
                        {
                            Instance.PrintWarning("Create message returned 404. Please confirm webhook url in config is correct.");
                            return;
                        }

                        if (response == null)
                        {
                            Instance.PrintWarning($"Created message returned null. Code: {code}");
                            return;
                        }

                        Instance.storedData.LastMessageId = (string)JObject.Parse(response)?["id"] ?? string.Empty;
                        Interface.Oxide.DataFileSystem.WriteObject(Instance.Name, Instance.storedData);

                    }, Instance,
                    Core.Libraries.RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } }, 10F);
            }
            else
            {
                Instance.webrequest.Enqueue(
                    StringBuilderInstance.AppendFormat(DISCORD_MSG_UPDATE, Instance.Configuration.WebHookForSendingNotificationsToDiscord, Instance.storedData.LastMessageId).ToString(),
                    embeds, (code, response) =>
                    {
                        if (code != 200)
                        {
                            Instance.storedData.LastMessageId = string.Empty;
                            SendOrUpdateDiscordMessage(message, title);
                        }
                    }, Instance,
                    Core.Libraries.RequestMethod.PATCH, new Dictionary<string, string> { { "Content-Type", "application/json" } }, 10F);
            }
        }

        private static string GenerateEmbeds(string message, string title)
        {
            List<Fields> fields = new List<Fields> { new Fields(title, message, true), };

           return new FancyMessage(null, new FancyMessage.Embeds[1]
            {
                new FancyMessage.Embeds(Instance.Title, 2105893, DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz"), fields,
                    new Footer("lone.design", "https://lone.design/wp-content/uploads/2020/03/cropped-Blackwolf-32x32.png"),
                    new Thumbnail("https://lone.design/wp-content/uploads/2022/01/update-checker-logo.png"))
            }).toJSON();
        }
        public class FancyMessage
        {
            public string content { get; set; }
            public Embeds[] embeds { get; set; }
            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public string timestamp { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Thumbnail thumbnail { get; set; }

                public Embeds(string title, int color, string timestamp, List<Fields> fields, Footer footer, Thumbnail thumbnail)
                {
                    this.title = title;
                    this.color = color;
                    this.timestamp = timestamp;
                    this.fields = fields;
                    this.footer = footer;
                    this.thumbnail = thumbnail;
                }
            }
            public FancyMessage(string content, Embeds[] embeds)
            {
                this.content = content;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }

        public class Footer
        {
            public string text { get; set; }
            public string icon_url { get; set; }
            public Footer(string text, string icon_url)
            {
                this.text = text;
                this.icon_url = icon_url;
            }
        }

        public class Thumbnail
        {
            public string url { get; set; }
            public Thumbnail(string url)
            {
                this.url = url;
            }
        }

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        #endregion

        #region STATIC HELPERS
        public static VersionNumber VersionNumberFromString(string versionString)
        {
            var split = versionString.Split('.');

            if (split.Length != 3)
            {
                return InvalidVersionNumber;
            }

            ushort major;
            ushort minor;
            ushort patch;

            if (!ushort.TryParse(split[0], out major))
            {
                return InvalidVersionNumber;
            }

            if (!ushort.TryParse(split[1], out minor))
            {
                return InvalidVersionNumber;
            }

            if (!ushort.TryParse(split[2], out patch))
            {
                return InvalidVersionNumber;
            }

            return new VersionNumber(major, minor, patch);
        }
        public static bool IsObjectNull(object obj) => ReferenceEquals(obj, null);
        #endregion

        #region DATA
        private class StoredData
        {
            public string LastMessageId { get; set; }
            public bool DoesItExistMessageId()
            {
                return string.IsNullOrEmpty(LastMessageId);
            }
        }
        #endregion
    }
}