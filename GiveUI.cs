using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GiveUI", "sami37", "1.0.0")]
    [Description("GiveUI")]
    public class GiveUI : RustPlugin
    {
        Dictionary<string, List<int>> ItemList = new Dictionary<string, List<int>>();
        List<object> blackList = new List<object>();
        private bool newConfig = false;
        private string GiveOverlayName = "giveContainer";
        static string dataDirectory = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}unitycore{Path.DirectorySeparatorChar}Images{Path.DirectorySeparatorChar}";
        configs checkConf;

        [PluginReference]
        Plugin ImageLibrary;

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());

        bool hasAccess(BasePlayer player, string permissionName)
        {
            if (player.net.connection.authLevel > 1)
                return true;
            return permission.UserHasPermission(player.userID.ToString(), permissionName);
        }

        class configs : MonoBehaviour
        {
            private GiveUI instance;
            void Awake()
            {
                instance = new GiveUI();
                instance.LoadItemList();
                InvokeRepeating("CheckItemList", 2f, 1.5f);
            }

            void OnDestroy()
            {
                CancelInvoke("CheckItemList");
            }
        }


        void LoadItemList()
        {
            try
            {
                if (ItemManager.itemList != null)
                {
                    List<ItemDefinition> itemList = ItemManager.itemList;

                    foreach (ItemDefinition itemDef in itemList)
                    {
                        if(ItemList == null)
                            ItemList = new Dictionary<string, List<int>>();
                        if (!ItemList.ContainsKey(itemDef.category.ToString()))
                        {
                            ItemList.Add(itemDef.category.ToString(), new List<int>());
                        }
                        ItemList[itemDef.category.ToString()].Add(itemDef.itemid);
                    }
                }
            }
            catch (Exception e)
            {
                Puts(e.Message);
                throw;
            }
        }

        void CheckItemList()
        {
            if (ItemManager.itemList != null && ItemList != null)
                GameObject.Destroy(checkConf);
        }

        void Loaded()
        {
            checkConf = new configs();
            permission.RegisterPermission("giveui.use", this);
            LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadConfig();
        }

        void LoadConfig()
        {
            SetConfig("Blacklist", "Item", new List<object>{2115555558, -533875561});
            blackList = GetConfig(new List<object>{2115555558, -533875561}, "Blacklist", "Item");
            if (!newConfig) return;
            SaveConfig();
            newConfig = false;
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
            }
        }

        private void OnServerInitialized()
        {
            var messages = new Dictionary<string, string>
            {
				{"NoPerm", "You don't have permission to do this."},
                {"GaveSelf", "You gave {0} x {1} to yourself."},
                {"GaveTo", "You gave {0} x {1} to {2}."}
            };
            lang.RegisterMessages(messages, this);
            LoadItemList();
        }
        public string GetImage(string shortname) => (string)ImageLibrary.Call("GetImage", shortname, 0);
        public void AddImage(string shortname)
        {
                string url = shortname;
                if (!url.StartsWith("http") && !url.StartsWith("www") && !url.StartsWith("file://"))
                    url = $"{dataDirectory}{shortname}.png";
            ImageLibrary.Call("AddImage", url, shortname, 0);
        }

        private void SendMSG(BasePlayer player, string msg, string keyword = "title")
        {
            SendReply(player, msg);
        }

        [ChatCommand("giveui")]
        void cmdGiveUIChat(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player, "giveui.use"))
            {
                SendMSG(player, lang.GetMessage("NoPerm", this));
                return;
            }
            if (args.Length == 0)
            {
                CreateUI(player);
            }
            if (args.Length > 1)
            {
                if (args[0].ToLower() != "self")
                {
                    var ppl = BasePlayer.Find(args[0]);
                    if (ppl == null)
                    {
                        SendMSG(player, "Can't find player " + args[0]);
                        return;
                    }
                }
                CreateUI(player, args[0], args[1]);
            }
            else
                SendMSG(player, "Syntax: /giveui <self|playerName|playerID> <amount>");

        }

        [ConsoleCommand("giveui")]
        void cmdGiveUIConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            if (arg.Args != null && arg.Args.Length > 2)
            {
                CreateUI(arg.Player(), arg.Args[0], arg.Args[1], arg.Args[2]);
            }
            else
            {
                CreateUI(arg.Player());
            }
        }

        [ConsoleCommand("giveitem")]
        void cmdGiveItemConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            var item = ItemManager.CreateByName(arg.Args[0], Convert.ToInt32(arg.Args[1]));
            if(!item.MoveToContainer(arg.Player().inventory.containerMain) && !item.MoveToContainer(arg.Player().inventory.containerBelt))
                item.Drop(arg.Player().eyes.position, arg.Player().eyes.BodyForward()*2f);
            SendMSG(arg.Player(), string.Format(lang.GetMessage("GaveSelf", this, arg.Player().UserIDString), item.info.displayName.english, item.amount));
        }
        
        [ConsoleCommand("giveitemTo")]
        void cmdGiveItemToConsole(ConsoleSystem.Arg arg)
        {
            var player = BasePlayer.Find(arg.Args[2]);
            if (player == null) return;
            var item = ItemManager.CreateByName(arg.Args[0], Convert.ToInt32(arg.Args[1]));
            if(!item.MoveToContainer(player.inventory.containerMain) && !item.MoveToContainer(player.inventory.containerBelt))
                item.Drop(player.eyes.position, player.eyes.BodyForward()*2f);
            SendMSG(arg.Player(), string.Format(lang.GetMessage("GaveTo", this, arg.Player().UserIDString), item.info.displayName.english, item.amount, player.displayName));
        }

        void DestroyUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, GiveOverlayName);
        }
        void CreateUI(BasePlayer player, string target = "null", string amount = "10000", string tab = "Null")
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.8"},
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"},
                CursorEnabled = true
            }, new CuiElement().Parent, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Weapon",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.02 0.94", AnchorMax = "0.09 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Weapon"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Construction",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.10 0.94", AnchorMax = "0.16 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Construction"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Items",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.17 0.94", AnchorMax = "0.23 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Items"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Resources",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.24 0.94", AnchorMax = "0.30 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Resources"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Attire",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.31 0.94", AnchorMax = "0.37 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Attire"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Tool",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.38 0.94", AnchorMax = "0.44 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Tool"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Medical",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.45 0.94", AnchorMax = "0.51 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Medical"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Food",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.51 0.94", AnchorMax = "0.58 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Food"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Ammunition",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.59 0.94", AnchorMax = "0.65 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Ammunition"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Traps",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.66 0.94", AnchorMax = "0.72 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Traps"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Misc",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.73 0.94", AnchorMax = "0.79 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Misc"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "Components",
                    Align = TextAnchor.MiddleCenter,
                    Color = "255 255 255",
                    FontSize = 14
                },
                RectTransform = { AnchorMin = "0.80 0.94", AnchorMax = "0.86 0.98"},
                Button =
                {
                    Close = GiveOverlayName,
                    Color = "0.5 0.5 0.5 0.2",
                    Command = $"giveui {target} {amount} Component"
                }
            }, GiveOverlayName);
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = "DestroyUi", 
                    Close = GiveOverlayName, 
                    Color = "0.5 0.5 0.5 0.2"
                },
                RectTransform =
                {
                    AnchorMin = "0.87 0.94",
                    AnchorMax = "0.97 0.98"
                
                },
                Text =
                {
                    Text = "Close", 
                    FontSize = 20, 
                    Align = TextAnchor.MiddleCenter
                        
                }
            },
            GiveOverlayName);
            int indent = 0;
            string category = null;
            switch (CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tab.ToLower()))
            {
                case "Weapon":
                    category = ItemCategory.Weapon.ToString();
                    break;
                case "Construction":
                    category = ItemCategory.Construction.ToString();
                    break;
                case "Items":
                    category = ItemCategory.Items.ToString();
                    break;
                case "Resources":
                    category = ItemCategory.Resources.ToString();
                    break;
                case "Attire":
                    category = ItemCategory.Attire.ToString();
                    break;
                case "Tool":
                    category = ItemCategory.Tool.ToString();
                    break;
                case "Medical":
                    category = ItemCategory.Medical.ToString();
                    break;
                case "Food":
                    category = ItemCategory.Food.ToString();
                    break;
                case "Ammunition":
                    category = ItemCategory.Ammunition.ToString();
                    break;
                case "Traps":
                    category = ItemCategory.Traps.ToString();
                    break;
                case "Misc":
                    category = ItemCategory.Misc.ToString();
                    break;
                case "Component":
                    category = ItemCategory.Component.ToString();
                    break;
                default:
                    goto case "Weapon";
            }
            foreach (var item in ItemList[category])
            {
                if(blackList != null && blackList.Contains(item))
                    continue;
                var anchormin = "0.05 0.80";
                var anchormax = "0.15 0.90";
                var indentmin = "0.8";
                var indentmax = "0.9";
                if (indent < 12)
                {
                    indentmin = "0.80";
                    indentmax = "0.90";
                }
                else if (indent >= 12 && indent < 24)
                {

                    indentmin = "0.69";
                    indentmax = "0.79";
                }
                else if (indent >= 24 && indent < 36)
                {
                    indentmin = "0.58";
                    indentmax = "0.68";
                }
                else if (indent >= 36 && indent < 46)
                {
                    indentmin = "0.47";
                    indentmax = "0.57";
                }
                else if (indent >= 47 && indent < 57)
                {
                    indentmin = "0.46";
                    indentmax = "0.36";
                }
                switch (indent)
                {
                    case 0:
                    case 12:
                    case 24:
                    case 36:
                    case 48:
                        anchormin = $"0.02 {indentmin}";
                        anchormax = $"0.09 {indentmax}";
                        break;
                    case 1:
                    case 13:
                    case 25:
                    case 37:
                    case 49:
                        anchormin = $"0.10 {indentmin}";
                        anchormax = $"0.17 {indentmax}";
                        break;
                    case 2:
                    case 14:
                    case 26:
                    case 38:
                    case 50:
                        anchormin = $"0.18 {indentmin}";
                        anchormax = $"0.25 {indentmax}";
                        break;
                    case 3:
                    case 15:
                    case 27:
                    case 39:
                    case 51:
                        anchormin = $"0.26 {indentmin}";
                        anchormax = $"0.33 {indentmax}";
                        break;
                    case 4:
                    case 16:
                    case 28:
                    case 40:
                    case 52:
                        anchormin = $"0.34 {indentmin}";
                        anchormax = $"0.41 {indentmax}";
                        break;
                    case 5:
                    case 17:
                    case 29:
                    case 41:
                    case 53:
                        anchormin = $"0.42 {indentmin}";
                        anchormax = $"0.49 {indentmax}";
                        break;
                    case 6:
                    case 18:
                    case 30:
                    case 42:
                    case 54:
                        anchormin = $"0.50 {indentmin}";
                        anchormax = $"0.57 {indentmax}";
                        break;
                    case 7:
                    case 19:
                    case 31:
                    case 43:
                    case 55:
                        anchormin = $"0.58 {indentmin}";
                        anchormax = $"0.65 {indentmax}";
                        break;
                    case 8:
                    case 20:
                    case 32:
                    case 44:
                    case 56:
                        anchormin = $"0.66 {indentmin}";
                        anchormax = $"0.73 {indentmax}";
                        break;
                    case 9:
                    case 21:
                    case 33:
                    case 45:
                    case 57:
                        anchormin = $"0.74 {indentmin}";
                        anchormax = $"0.81 {indentmax}";
                        break;
                    case 10:
                    case 22:
                    case 34:
                    case 46:
                    case 58:
                        anchormin = $"0.82 {indentmin}";
                        anchormax = $"0.89 {indentmax}";
                        break;
                    case 11:
                    case 23:
                    case 35:
                    case 47:
                    case 59:
                        anchormin = $"0.90 {indentmin}";
                        anchormax = $"0.97 {indentmax}";
                        break;
                }
                var itemDetails = ItemManager.FindItemDefinition(item);
                if (itemDetails != null)
                {
                    var backgroundImage = CreateImage(GiveOverlayName, itemDetails.shortname, anchormin, anchormax);
                    container.Add(backgroundImage);
                    container.Add(new CuiButton
                    {
                        Text =
                        {
                            Text = itemDetails.displayName.english,
                            FontSize = 16,
                            Align = TextAnchor.LowerCenter
                        },
                        Button =
                        {
                            Color = "0.5 0.5 0.5 0",
                            Command = (target != "self" && target != "null" ? "giveitemTo " : "giveitem ") + itemDetails.shortname + " " + amount + " " + target
                        },
                        RectTransform =
                        {
                            AnchorMin = anchormin,
                            AnchorMax = anchormax
                        }
                    }, GiveOverlayName);
                    indent++;
                }
            }
            CuiHelper.AddUi(player, container);
        }
        private CuiElement CreateImage(string panelName, string png, string anchormin, string anchormax)
        {
			AddImage(png);
            png = GetImage(png);
            var element = new CuiElement();
            var image = new CuiRawImageComponent
            {
                Png = png,
                Color = "255 255 255 1"
            };
            var rectTransform = new CuiRectTransformComponent
            {
                AnchorMin = anchormin,
                AnchorMax = anchormax
            };
            element.Components.Add(image);
            element.Components.Add(rectTransform);
            element.Parent = panelName;

            return element;
        }
    }
}