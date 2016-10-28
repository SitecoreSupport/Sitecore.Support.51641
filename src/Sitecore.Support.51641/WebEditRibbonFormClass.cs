using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Pipelines;
using Sitecore.Pipelines.GetPageEditorNotifications;
using Sitecore.Pipelines.HasPresentation;
using Sitecore.Resources;
using Sitecore.SecurityModel;
using Sitecore.Shell;
using Sitecore.Shell.Framework;
using Sitecore.Shell.Framework.CommandBuilders;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Sites;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls.Ribbons;

namespace Sitecore.Support.Shell.Applications.WebEdit
{
    public class WebEditRibbonForm : BaseForm
    {
        /// <summary>
        /// The ribbon form.
        /// </summary>
        protected System.Web.UI.HtmlControls.HtmlForm RibbonForm;

        /// <summary>
        /// The ribbon pane.
        /// </summary>
        protected Border RibbonPane;

        /// <summary>
        /// The treecrumb.
        /// </summary>
        protected Border Treecrumb;

        /// <summary>
        /// The notifications.
        /// </summary>
        protected Border Notifications;

        /// <summary>
        /// The treecrumb pane.
        /// </summary>
        protected Border TreecrumbPane;

        /// <summary>
        /// The current item deleted.
        /// </summary>
        private bool currentItemDeleted;

        /// <summary>
        /// The refresh has been asked.
        /// </summary>
        private bool refreshHasBeenAsked;

        /// <summary>
        /// Gets or sets the URI of the context item.
        /// The item is affected by the treecrumb selection.
        /// </summary>
        /// <remarks>
        /// NOTE: URI is affected by what is currently selected in the treecrumb.
        /// Use for command context item checks.
        /// Don't use for "what item is currently open in the Page Editor" checks.
        /// </remarks>
        /// <value>
        /// The URI.
        /// </value>
        public string ContextUri
        {
            get
            {
                return (base.ServerProperties["ContextUri"] ?? this.CurrentItemUri) as string;
            }
            set
            {
                Assert.ArgumentNotNullOrEmpty(value, "value");
                base.ServerProperties["ContextUri"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the URI of the current item in the Page Editor.
        /// </summary>
        /// <remarks>
        /// CurrentItemUri is not affected by the treecrumb context changes.
        /// Use CurrentItemUri if you need to know which item is being viewed in the Page Editor.
        /// </remarks>
        /// <value>
        /// The current item URI.
        /// </value>
        public string CurrentItemUri
        {
            get
            {
                return base.ServerProperties["CurrentItemUri"] as string;
            }
            set
            {
                Assert.ArgumentNotNullOrEmpty(value, "value");
                base.ServerProperties["CurrentItemUri"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the URI of the context item.
        /// The item is affected by the treecrumb selection.
        /// </summary>
        /// <remarks>
        /// NOTE: Uri is affected by what is currently selected in the treecrumb.
        /// Use for command context item checks.
        /// Don't use for "what item is currently open in the Page Editor" checks.
        /// </remarks>
        /// <value>
        /// The URI.
        /// </value>
        [System.Obsolete("Use ContextUri")]
        public string Uri
        {
            get
            {
                return this.ContextUri;
            }
            set
            {
                this.ContextUri = value;
            }
        }

        /// <summary>
        /// Handles the message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            if (!this.VerifyWebeditLoaded())
            {
                SheerResponse.Alert("The Page Editor is not yet available.", new string[0]);
                message.CancelBubble = true;
                message.CancelDispatch = true;
                return;
            }
            if (message.Name == "item:save")
            {
                SiteContext site = Context.Site;
                Assert.IsNotNull(site, "Site not found.");
                site.Notifications.Disabled = MainUtil.GetBool(message.Arguments["disableNotifications"], false);
                message = Message.Parse(message.Sender, "webedit:save");
            }
            if (message.Name == "ribbon:update")
            {
                string uri = this.ContextUri;
                if (!string.IsNullOrEmpty(message.Arguments["id"]))
                {
                    ID itemID = new ID(message.Arguments["id"]);
                    Language language = Language.Parse(message.Arguments["lang"]);
                    Sitecore.Data.Version version = new Sitecore.Data.Version(message.Arguments["ver"]);
                    string databaseName = message.Arguments["db"];
                    uri = new ItemUri(itemID, language, version, databaseName).ToString();
                }
                this.Update(uri);
                return;
            }
            if (message.Name == "item:refresh")
            {
                string contextUri = this.ContextUri;
                this.Update(contextUri);
                return;
            }
            Dispatcher.Dispatch(message, this.GetCurrentItem(message));
        }

        /// <summary>
        /// Confirms and reloads.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected void ConfirmAndReload(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (this.currentItemDeleted)
            {
                return;
            }
            if (args.IsPostBack)
            {
                if (args.HasResult && args.Result != "no")
                {
                    SheerResponse.Eval("window.parent.location.reload(true)");
                    return;
                }
            }
            else if (!this.refreshHasBeenAsked)
            {
                SheerResponse.Confirm("An item was deleted. Do you want to refresh the page?");
                this.refreshHasBeenAsked = true;
                args.WaitForPostBack();
            }
        }

        /// <summary>
        /// The copied notification.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected virtual void CopiedNotification(object sender, ItemCopiedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Context.ClientPage.Start(this, "Reload");
        }

        /// <summary>
        /// The created notification.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected virtual void CreatedNotification(object sender, ItemCreatedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Context.ClientPage.Start(this, "Reload");
        }

        /// <summary>
        /// The deleted notification.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected virtual void DeletedNotification(object sender, ItemDeletedEventArgs args)
        {

            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Assert.IsNotNull(args.Item, "Deleted item in DeletedNotification args.");
            ItemUri itemUri = ItemUri.Parse(CurrentItemUri);
            Assert.IsNotNull(itemUri, "uri");
            Item originalItem = args.Item.Database.GetItem(args.Item.ID, itemUri.Language);

            if (originalItem == null)
            {
                SheerResponse.Eval("window.parent.location.reload(true);");
            }

            Item item = args.Item.Database.GetItem(args.ParentID, itemUri.Language);
            if (item != null)
            {
                if (itemUri.ItemID == args.Item.ID && itemUri.DatabaseName == args.Item.Database.Name)
                {
                    this.currentItemDeleted = true;
                    this.Redirect(WebEditRibbonForm.GetTarget(item));
                    return;
                }
                Item item2 = Database.GetItem(itemUri);
                if (item2 != null && !this.currentItemDeleted)
                {
                    // Context.ClientPage.Start(this, "ConfirmAndReload");
                    SheerResponse.Eval("window.parent.location.reload(true);");
                }
            }
        }

        /// <summary>
        /// Gets the context item. The context item is affected by the treecrumb selection.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <returns>
        /// The current item.
        /// </returns>
        protected virtual Item GetCurrentItem(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string text = message["id"];
            if (string.IsNullOrEmpty(this.CurrentItemUri))
            {
                return null;
            }
            ItemUri itemUri = ItemUri.Parse(this.CurrentItemUri);
            if (itemUri == null)
            {
                return null;
            }
            Item item = Database.GetItem(itemUri);
            if (!string.IsNullOrEmpty(text) && item != null)
            {
                return item.Database.GetItem(text, item.Language);
            }
            return item;
        }

        /// <summary>
        /// Determines whether user is simple.
        /// </summary>
        /// <returns>
        /// <c>true</c> if user is simple; otherwise, <c>false</c>.
        /// </returns>
        protected virtual bool IsSimpleUser()
        {
            return false;
        }

        /// <summary>
        /// The moved notification.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected virtual void MovedNotification(object sender, ItemMovedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            string currentItemUri = this.CurrentItemUri;
            if (string.IsNullOrEmpty(currentItemUri))
            {
                return;
            }
            ItemUri itemUri = ItemUri.Parse(currentItemUri);
            if (itemUri == null)
            {
                return;
            }
            if (!(args.Item.ID == itemUri.ItemID) || !(args.Item.Database.Name == itemUri.DatabaseName))
            {
                Context.ClientPage.Start(this, "Reload");
                return;
            }
            Item item = Database.GetItem(itemUri);
            if (item == null)
            {
                Log.SingleError("Item not found after moving. Item uri:" + itemUri, this);
                return;
            }
            this.Redirect(item);
            WebEditRibbonForm.DisableOtherNotifications();
        }

        /// <summary>
        /// Raises the load event.
        /// </summary>
        /// <param name="e">
        /// The <see cref="T:System.EventArgs" /> instance containing the event data.
        /// </param>
        /// <remarks>
        /// This method notifies the server control that it should perform actions common to each HTTP
        /// request for the page it is associated with, such as setting up a database query. At this
        /// stage in the page life cycle, server controls in the hierarchy are created and initialized,
        /// view state is restored, and form controls reflect client-side data. Use the IsPostBack
        /// property to determine whether the page is being loaded in response to a client postback,
        /// or if it is being loaded and accessed for the first time.
        /// </remarks>
        protected override void OnLoad(System.EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            this.refreshHasBeenAsked = false;
            SiteContext site = Context.Site;
            if (site != null)
            {
                site.Notifications.ItemDeleted += new ItemDeletedDelegate(this.DeletedNotification);
                site.Notifications.ItemMoved += new ItemMovedDelegate(this.MovedNotification);
                site.Notifications.ItemRenamed += new ItemRenamedDelegate(this.RenamedNotification);
                site.Notifications.ItemCopied += new ItemCopiedDelegate(this.CopiedNotification);
                site.Notifications.ItemCreated += new ItemCreatedDelegate(this.CreatedNotification);
                site.Notifications.ItemSaved += new ItemSavedDelegate(this.SavedNotification);
            }
            if (Context.ClientPage.IsEvent)
            {
                string currentItemUri = this.CurrentItemUri;
                if (!string.IsNullOrEmpty(currentItemUri))
                {
                    Item item;
                    using (new SecurityDisabler())
                    {
                        item = Database.GetItem(new ItemUri(currentItemUri));
                    }
                    if (item == null)
                    {
                        SheerResponse.Eval("scShowItemDeletedNotification(\"" + Translate.Text("The item does not exist. It may have been deleted by another user.") + "\")");
                        return;
                    }
                    if (Database.GetItem(new ItemUri(currentItemUri)) == null)
                    {
                        SheerResponse.Eval("scShowItemDeletedNotification(\"" + Translate.Text("The item could not be found.\n\nYou may not have read access or it may have been deleted by another user.").Replace('\n', ' ') + "\")");
                    }
                }
                return;
            }
            ItemUri itemUri = ItemUri.ParseQueryString();
            Assert.IsNotNull(itemUri, typeof(ItemUri));
            this.CurrentItemUri = itemUri.ToString();
            Item item2 = Database.GetItem(itemUri);
            if (item2 == null)
            {
                WebUtil.RedirectToErrorPage(Translate.Text("The item could not be found.\n\nYou may not have read access or it may have been deleted by another user."));
                return;
            }
            this.RenderRibbon(item2);
            this.RenderTreecrumb(item2);
            this.RenderNotifications(item2);
            this.RibbonForm.Attributes["class"] = UIUtil.GetBrowserClassString();
        }

        /// <summary>
        /// Performs the redirection.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected void Redirect(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            string text = args.Parameters["url"];
            Assert.IsNotNullOrEmpty(text, "url");
            SheerResponse.Eval(string.Format("window.parent.location.href='{0}'", text));
        }

        /// <summary>
        /// Performs the reload.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected void Reload(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (!UIUtil.IsFirefox())
            {
                SheerResponse.Eval("window.parent.location.reload(true)");
            }
        }

        /// <summary>
        /// The renamed notification.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected virtual void RenamedNotification(object sender, ItemRenamedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            ItemUri itemUri = ItemUri.Parse(this.CurrentItemUri);
            Assert.IsNotNull(itemUri, "uri");
            if (itemUri.ItemID == args.Item.ID && itemUri.DatabaseName == args.Item.Database.Name)
            {
                Item item = args.Item.Database.GetItem(args.Item.ID, itemUri.Language);
                if (item != null)
                {
                    this.Redirect(item);
                    return;
                }
            }
            Context.ClientPage.Start(this, "Reload");
        }

        /// <summary>
        /// Renders the ribbon.
        /// </summary>
        /// <param name="item">
        /// The item.
        /// </param>
        protected virtual void RenderRibbon(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            string queryString = WebUtil.GetQueryString("mode");
            Ribbon ribbon = new Ribbon
            {
                ID = "Ribbon",
                ShowContextualTabs = false,
                ActiveStrip = ((queryString == "preview") ? "VersionStrip" : WebUtil.GetCookieValue("sitecore_webedit_activestrip"))
            };
            string a;
            string path;
            if ((a = queryString) != null)
            {
                if (a == "preview")
                {
                    path = "/sitecore/content/Applications/WebEdit/Ribbons/Preview";
                    goto IL_A2;
                }
                if (a == "edit")
                {
                    path = (this.IsSimpleUser() ? "/sitecore/content/Applications/WebEdit/Ribbons/Simple" : "/sitecore/content/Applications/WebEdit/Ribbons/WebEdit");
                    goto IL_A2;
                }
            }
            path = "/sitecore/content/Applications/WebEdit/Ribbons/Debug";
            IL_A2:
            SiteRequest request = Context.Request;
            Assert.IsNotNull(request, "Site request not found.");
            CommandContext commandContext = new CommandContext(item);
            commandContext.Parameters["sc_pagesite"] = request.QueryString["sc_pagesite"];
            ribbon.CommandContext = commandContext;
            commandContext.RibbonSourceUri = new ItemUri(path, Context.Database);
            this.RibbonPane.InnerHtml = HtmlUtil.RenderControl(ribbon);
        }

        /// <summary>
        /// Renders the treecrumb.
        /// </summary>
        /// <param name="item">
        /// The item.
        /// </param>
        protected virtual void RenderTreecrumb(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            if (this.IsSimpleUser() || WebUtil.GetQueryString("debug") == "1")
            {
                this.Treecrumb.Visible = false;
                return;
            }
            System.Web.UI.HtmlTextWriter htmlTextWriter = new System.Web.UI.HtmlTextWriter(new System.IO.StringWriter());
            this.RenderTreecrumb(htmlTextWriter, item);
            this.RenderTreecrumbGo(htmlTextWriter, item);
            if (WebUtil.GetQueryString("mode") != "preview")
            {
                this.RenderTreecrumbEdit(htmlTextWriter, item);
            }
            this.Treecrumb.InnerHtml = htmlTextWriter.InnerWriter.ToString();
        }

        /// <summary>
        /// Renders the treecrumb.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        protected virtual void RenderTreecrumb(System.Web.UI.HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            Item parent = item.Parent;
            if (parent != null && parent.ID != ItemIDs.RootID)
            {
                this.RenderTreecrumb(output, parent);
            }
            this.RenderTreecrumbLabel(output, item);
            this.RenderTreecrumbGlyph(output, item);
        }

        /// <summary>
        /// Renders the treecrumb edit.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        protected virtual void RenderTreecrumbEdit(System.Web.UI.HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            bool flag = Policy.IsAllowed("Page editor/Navigation bar/Can Edit");
            if (flag)
            {
                Command command = CommandManager.GetCommand("webedit:open");
                if (command != null)
                {
                    CommandState commandState = CommandManager.QueryState(command, new CommandContext(item));
                    flag = (commandState != CommandState.Hidden && commandState != CommandState.Disabled);
                }
            }
            if (flag)
            {
                CommandBuilder commandBuilder = new CommandBuilder("webedit:open");
                commandBuilder.Add("id", item.ID.ToString());
                string clientEvent = Context.ClientPage.GetClientEvent(commandBuilder.ToString());
                output.Write("<a href=\"javascript:void(0)\" onclick=\"{0}\" class=\"scTreecrumbGo\">", clientEvent);
            }
            else
            {
                output.Write("<span class=\"scTreecrumbGo\">");
            }
            ImageBuilder arg = new ImageBuilder
            {
                Src = "ApplicationsV2/16x16/edit.png",
                Class = "scTreecrumbGoIcon",
                Disabled = !flag
            };
            output.Write("{0} {1}{2}", arg, Translate.Text("Edit"), flag ? "</a>" : "</span>");
        }

        /// <summary>
        /// Renders the treecrumb glyph.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        protected virtual void RenderTreecrumbGlyph(System.Web.UI.HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            if (!item.HasChildren)
            {
                return;
            }
            if (Context.Device == null)
            {
                return;
            }
            DataContext dataContext = new DataContext
            {
                DataViewName = "Master"
            };
            ItemCollection children = dataContext.GetChildren(item);
            if (children == null || children.Count == 0)
            {
                return;
            }
            ShortID arg = ID.NewID.ToShortID();
            string arg2 = string.Format("javascript:scContent.showOutOfFrameGallery(this, event, \"Gallery.ItemChildren\", {{height: 30, width: 30 }}, {{itemuri: \"{0}\" }});", item.Uri);
            ImageBuilder arg3 = new ImageBuilder
            {
                Src = "Images/ribboncrumb16x16.png",
                Class = "scTreecrumbChevronGlyph"
            };
            output.Write("<a id=\"L{0}\" class=\"scTreecrumbChevron\" href=\"#\" onclick='{1}'>{2}</a>", arg, arg2, arg3);
        }

        /// <summary>
        /// Renders the treecrumb go.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        protected virtual void RenderTreecrumbGo(System.Web.UI.HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            output.Write("<div class=\"scTreecrumbDivider\">{0}</div>", Images.GetSpacer(1, 1));
            bool flag = HasPresentationPipeline.Run(item);
            if (flag)
            {
                output.Write("<a href=\"{0}\" class=\"scTreecrumbGo\" target=\"_parent\">", Sitecore.Web.WebEditUtil.GetItemUrl(item));
            }
            else
            {
                output.Write("<span class=\"scTreecrumbGo\">");
            }
            ImageBuilder arg = new ImageBuilder
            {
                Src = "ApplicationsV2/16x16/arrow_right_green.png",
                Class = "scTreecrumbGoIcon",
                Disabled = !flag
            };
            output.Write("{0} {1}{2}", arg, Translate.Text("Go"), flag ? "</a>" : "</span>");
        }

        /// <summary>
        /// Renders the treecrumb label.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        /// <param name="item">
        /// The item.
        /// </param>
        protected virtual void RenderTreecrumbLabel(System.Web.UI.HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            Item parent = item.Parent;
            if (parent == null || parent.ID == ItemIDs.RootID)
            {
                return;
            }
            string arg = string.Format("javascript:scForm.postRequest(\"\",\"\",\"\",{0})", StringUtil.EscapeJavascriptString(string.Format("Update(\"{0}\")", item.Uri)));
            output.Write("<a class=\"scTreecrumbNode\" href=\"#\" onclick='{0}'>", arg);
            string text = "scTreecrumbNodeLabel";
            if (item.Uri.ToString() == this.CurrentItemUri)
            {
                text += " scTreecrumbNodeCurrentItem";
            }
            output.Write("<span class=\"{0}\">{1}</span></a>", text, item.DisplayName);
        }

        /// <summary>
        /// The saved notification.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        protected virtual void SavedNotification(object sender, ItemSavedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (!Context.PageDesigner.IsDesigning && !args.Changes.Renamed)
            {
                Context.ClientPage.Start(this, "Reload");
            }
        }

        /// <summary>
        /// Shows the subitems.
        /// </summary>
        /// <param name="uri">
        /// The URI.
        /// </param>
        [System.Obsolete("Deprecated.")]
        protected void ShowSubitems(string uri)
        {
            Assert.ArgumentNotNullOrEmpty(uri, "uri");
            ItemUri itemUri = ItemUri.Parse(uri);
            if (itemUri == null)
            {
                return;
            }
            this.ContextUri = itemUri.ToString();
            Item item = Database.GetItem(itemUri);
            if (item == null)
            {
                return;
            }
            SheerResponse.DisableOutput();
            Menu menu = new Menu();
            foreach (Item item2 in item.Children)
            {
                if (!item2.Appearance.Hidden || UserOptions.View.ShowHiddenItems)
                {
                    menu.Add(item2.Appearance.DisplayName, item2.Appearance.Icon, string.Format("Update(\"{0}\")", item2.Uri));
                }
            }
            SheerResponse.EnableOutput();
            SheerResponse.ShowPopup(Context.ClientPage.ClientRequest.Source, "below", menu);
        }

        /// <summary>
        /// Updates the specified URI.
        /// </summary>
        /// <param name="uri">
        /// The URI.
        /// </param>
        protected void Update(string uri)
        {
            ItemUri itemUri = string.IsNullOrEmpty(uri) ? ItemUri.ParseQueryString() : ItemUri.Parse(uri);
            if (itemUri == null)
            {
                return;
            }
            this.ContextUri = itemUri.ToString();
            Item item = Database.GetItem(itemUri);
            if (item == null || this.CurrentItemUri == null)
            {
                return;
            }
            ItemUri itemUri2 = ItemUri.Parse(this.CurrentItemUri);
            if (itemUri2 == null)
            {
                return;
            }
            Item item2 = Database.GetItem(itemUri2);
            if (item2 == null)
            {
                return;
            }
            this.RenderRibbon(item2);
            this.RenderTreecrumb(item);
            SheerResponse.Eval("scAdjustPositioning()");
        }

        /// <summary>
        /// Verifies the webedit loaded.
        /// </summary>
        /// <returns>
        /// The verification result.
        /// </returns>
        protected virtual bool VerifyWebeditLoaded()
        {
            return !string.IsNullOrEmpty(this.ContextUri);
        }

        /// <summary>
        /// Disables the other notifications.
        /// </summary>
        private static void DisableOtherNotifications()
        {
            SiteContext site = Context.Site;
            if (site != null)
            {
                site.Notifications.Disabled = true;
            }
        }

        /// <summary>
        /// Gets the notification icon.
        /// </summary>
        /// <param name="notificationType">
        /// Type of the notification.
        /// </param>
        /// <returns>
        /// The notification icon.
        /// </returns>
        private static string GetNotificationIcon(PageEditorNotificationType notificationType)
        {
            switch (notificationType)
            {
                case PageEditorNotificationType.Error:
                    return "Custom/16x16/error.png";
                case PageEditorNotificationType.Information:
                    return "Custom/16x16/info.png";
                default:
                    return "Custom/16x16/warning.png";
            }
        }

        /// <summary>
        /// Gets the target item.
        /// </summary>
        /// <param name="parent">
        /// The parent item.
        /// </param>
        /// <returns>
        /// The target item.
        /// </returns>
        private static Item GetTarget(Item parent)
        {
            Assert.ArgumentNotNull(parent, "parent");
            if (HasPresentationPipeline.Run(parent))
            {
                return parent;
            }
            string siteName = Sitecore.Web.WebEditUtil.SiteName;
            SiteContext site = SiteContext.GetSite(siteName);
            if (site == null)
            {
                return parent;
            }
            string contentStartPath = site.ContentStartPath;
            Item item = parent.Database.GetItem(contentStartPath, parent.Language);
            return item ?? parent;
        }

        /// <summary>
        /// Groups the notifications by type.
        /// </summary>
        /// <param name="notifications">
        /// The notifications.
        /// </param>
        /// <returns>
        /// The notifications grouped by type.
        /// </returns>
        private static System.Collections.Generic.List<PageEditorNotification> GroupNotifications(System.Collections.Generic.IEnumerable<PageEditorNotification> notifications)
        {
            Assert.ArgumentNotNull(notifications, "notifications");
            System.Collections.Generic.List<PageEditorNotification> list = new System.Collections.Generic.List<PageEditorNotification>();
            list.AddRange(from n in notifications
                          where n.Type == PageEditorNotificationType.Error
                          select n);
            list.AddRange(from n in notifications
                          where n.Type == PageEditorNotificationType.Warning
                          select n);
            list.AddRange(from n in notifications
                          where n.Type == PageEditorNotificationType.Information
                          select n);
            return list;
        }

        /// <summary>
        /// Renders the notification.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        /// <param name="notification">
        /// The notification.
        /// </param>
        /// <param name="marker">
        /// The marker.
        /// </param>
        private static void RenderNotification(System.Web.UI.HtmlTextWriter output, PageEditorNotification notification, string marker)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(notification, "notification");
            Assert.ArgumentNotNull(marker, "marker");
            string arg = Themes.MapTheme(notification.Icon ?? WebEditRibbonForm.GetNotificationIcon(notification.Type));
            output.Write("<div class=\"scPageEditorNotification {0}{1}\">", notification.Type, marker);
            output.Write("<img class=\"Icon\" src=\"{0}\"/>", arg);
            output.Write("<div class=\"Description\">{0}</div>", notification.Description);
            System.Collections.Generic.List<PageEditorNotificationOption> options = notification.Options;
            foreach (PageEditorNotificationOption current in options)
            {
                output.Write("<a onclick=\"javascript: return scForm.postEvent(this, event, '{0}')\" href=\"#\" class=\"OptionTitle\">{1}</a>", current.Command, current.Title);
            }
            output.Write("<br style=\"clear: both\"/>");
            output.Write("</div>");
        }

        /// <summary>
        /// Redirects to the specified item.
        /// </summary>
        /// <param name="item">
        /// The item.
        /// </param>
        private void Redirect(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            string itemUrl = Sitecore.Web.WebEditUtil.GetItemUrl(item);
            Context.ClientPage.Start(this, "Redirect", new NameValueCollection
            {
                {
                    "url",
                    itemUrl
                }
            });
        }

        /// <summary>
        /// Renders the notifications.
        /// </summary>
        /// <param name="item">
        /// The item.
        /// </param>
        private void RenderNotifications(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            if (WebUtil.GetQueryString("mode") != "edit")
            {
                return;
            }
            GetPageEditorNotificationsArgs getPageEditorNotificationsArgs = new GetPageEditorNotificationsArgs(item);
            CorePipeline.Run("getPageEditorNotifications", getPageEditorNotificationsArgs);
            System.Collections.Generic.List<PageEditorNotification> list = getPageEditorNotificationsArgs.Notifications;
            if (list.Count == 0)
            {
                this.Notifications.Visible = false;
                return;
            }
            list = WebEditRibbonForm.GroupNotifications(list);
            System.Web.UI.HtmlTextWriter htmlTextWriter = new System.Web.UI.HtmlTextWriter(new System.IO.StringWriter());
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                PageEditorNotification notification = list[i];
                string text = string.Empty;
                if (i == 0)
                {
                    text += " First";
                }
                if (i == count - 1)
                {
                    text += " Last";
                }
                WebEditRibbonForm.RenderNotification(htmlTextWriter, notification, text);
            }
            this.Notifications.InnerHtml = htmlTextWriter.InnerWriter.ToString();
        }
    }

}