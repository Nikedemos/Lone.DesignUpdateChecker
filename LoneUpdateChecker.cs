using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Lone.Design Update Checker", "Nikedemos / DezLife / nivex", "1.2.3")]
    [Description("Checks for available updates of Lone.Design plugins")]
    public class LoneUpdateChecker : RustPlugin
    {
        #region CONST/STATIC
        public const string API_URL = "https://api.lone.design/";

        public const string DISCORD_MSG_UPDATE = "{0}/messages/{1}";
        public const string DISCORD_MSG_CREATE = "{0}?wait=true";

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
            [MSG_PLUGIN_RESPONSE_SINGLE_PLUGIN_UP_TO_DATE] = "{0} is up to date."

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

            RequestSend();

            if (Configuration.CheckForUpdatesPeriodically)
            {
                var everyMinute = Configuration.HowManyMinutesBetweenPeriodicalUpdates;
                Instance.PrintWarning(MSG(MSG_UPDATE_CHECK_FREQUENCY, null, everyMinute));
                RequestTimer = timer.Repeat(everyMinute * 60F, 14400, () => RequestSend());
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

            RequestSend($"{plugin.Name}.cs");

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
            catch(Exception e)
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
            public string Name;

            public string PluginName;

            public string Version;
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

            string requestURL = BuildRequestURLWhilePopulatingPluginList(requestPlugins);

            Instance.webrequest.Enqueue(requestURL, string.Empty, callback, Instance, Core.Libraries.RequestMethod.GET, null, 10F);
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

                if (!CurrentPluginVersions.ContainsKey(currentInfo.PluginName))
                {
                    continue;
                }

                versionPresent = CurrentPluginVersions[currentInfo.PluginName];
                versionFromAPI = VersionNumberFromString(currentInfo.Version);

                lastSingle = currentInfo.PluginName;
                lastVersionPresent = versionPresent.ToString();
                lastVersionFromAPI = versionFromAPI.ToString();

                if (versionFromAPI == InvalidVersionNumber)
                {
                    StringBuilderInstance.AppendLine(MSG(MSG_PLUGIN_REPONSE_INVALID_VERSION, null, currentInfo.PluginName, currentInfo.Version));
                    continue;
                }

                if (versionFromAPI > versionPresent)
                {
                    StringBuilderInstance.AppendLine(MSG(MSG_PLUGIN_RESPONSE_OUTDATED_VERSION, null, currentInfo.PluginName, versionPresent, versionFromAPI));
                    
                    outdatedPluginsFound++;
                } 
            }

            StringBuilderInstance.AppendLine();
            if (outdatedPluginsFound > 0)
            {
                Instance.PrintError(StringBuilderInstance.ToString().Replace("*", string.Empty));
                if (Instance.Configuration.EnableSendingNotificationsToDiscord)
                    SendDiscordMessage(StringBuilderInstance.ToString(), single ? MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_SINGLE, null, lastSingle, lastVersionPresent, lastVersionFromAPI) : MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_BULK));

               Instance.PrintWarning(single ? MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_SINGLE, null, lastSingle, lastVersionPresent, lastVersionFromAPI) : MSG(MSG_PLUGIN_RESPONSE_NEEDS_UPDATE_BULK));
            }
            else
            {
                Instance.PrintWarning(single ? MSG(MSG_PLUGIN_RESPONSE_SINGLE_PLUGIN_UP_TO_DATE, null, lastSingle) : MSG(MSG_PLUGIN_RESPONSE_ALL_PLUGINS_UP_TO_DATE));
            }

        }

        public static void RequestCallbackSingle(int code, string response) => RequestCallbackCommon(code, response, true);

        public static void RequestCallbackBulk(int code, string response) => RequestCallbackCommon(code, response, false);

        public static string BuildRequestURLWhilePopulatingPluginList(string filterByName = default(string))
        {
            StringBuilderInstance.Clear();
            StringBuilderInstance.Append(API_URL);
            StringBuilderInstance.Append("search/");

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

                if (filterByName != default(string))
                {
                    if (!shortFilename.Contains(filterByName))
                    {
                        continue;
                    }
                }

                StringBuilderInstance.Append(shortFilename);

                CurrentPluginVersions.Add(shortFilename, currentPlugin.Version);

                if (i < iterateOver.Length-1)
                {
                    StringBuilderInstance.Append(",");
                }
            }

            return StringBuilderInstance.ToString();
        }
        #endregion

        #region DISCORD
        public static void SendDiscordMessage(string message, string title)
        {
            List<Fields> fields = new List<Fields> { new Fields(title, message, true),};

            StringBuilderInstance.Clear();
            bool data = Instance.storedData.DoesItExistMessageId();
            Core.Libraries.RequestMethod requestMethod = data == false ? Core.Libraries.RequestMethod.PATCH : Core.Libraries.RequestMethod.POST;
            string uri = data ? StringBuilderInstance.AppendFormat(DISCORD_MSG_CREATE, Instance.Configuration.WebHookForSendingNotificationsToDiscord).ToString() : StringBuilderInstance.AppendFormat(DISCORD_MSG_UPDATE, Instance.Configuration.WebHookForSendingNotificationsToDiscord, Instance.storedData.LastMessageId).ToString();
            Instance.webrequest.Enqueue(uri, 
                new FancyMessage(null, new FancyMessage.Embeds[1] { 
                    new FancyMessage.Embeds(Instance.Title, 2105893, DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz"), fields, 
                    new Footer("lone.design", "https://lone.design/wp-content/uploads/2020/03/cropped-Blackwolf-32x32.png"), 
                    new Thumbnail("https://lone.design/wp-content/uploads/2022/01/update-checker-logo.png"))}).toJSON(), (code, response) => 
                    {
                        if(code == 200 && response != null)
                        {
                            Instance.storedData.LastMessageId = (string)JObject.Parse(response)["id"];
                        }
                        else
                        {
                            Instance.storedData.LastMessageId = string.Empty;
                            SendDiscordMessage(message, title);
                        }
                    }, Instance,
            requestMethod, new Dictionary<string, string> { { "Content-Type", "application/json" } }, 10F);
        }
        public class FancyMessage
        {
            public string content {get; set;}
            public Embeds[] embeds {get; set;}
            public class Embeds
            {
                public string title {get; set;}
                public int color {get; set;}
                public string timestamp { get; set; }
                public List<Fields> fields {get; set;}
                public Footer footer {get; set;}
                public Thumbnail thumbnail { get; set;}

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
            public string text {get; set;}
            public string icon_url {get; set;}
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
            public string name {get; set;}
            public string value {get; set;}
            public bool inline {get; set;}
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
