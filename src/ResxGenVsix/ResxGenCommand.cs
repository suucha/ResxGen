using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace ResxGenVsix
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class ResxGenCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("e7bf6cf9-1194-4aef-86f5-3f07c44ae1b0");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResxGenCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private ResxGenCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }
        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static ResxGenCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in ResxGenCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ResxGenCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE80.DTE2;
            var project = dte.SelectedItems.Item(1).Project;

            if (project == null)
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    "请选择一个项目。",
                    "错误",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            var solution = workspace.CurrentSolution;
            var langs = new string[] { };
            var projectFullPath = Path.GetDirectoryName(project.FullName);
            var configFile = Path.Combine(projectFullPath, "ResxGen.json");
            var isFileStyle = false;
            var resourcesPath = "Resources";
            if (File.Exists(configFile))
            {
                try
                {
                    var configJson = File.ReadAllText(configFile);
                    var config = System.Text.Json.JsonSerializer.Deserialize<ResxGenConfig>(configJson);
                    if (config.IsFileStyle != null && config.IsFileStyle.Value)
                    {
                        isFileStyle = true;
                    }
                    if (!string.IsNullOrEmpty(config.ResourcesPath))
                    {
                        resourcesPath = config.ResourcesPath;
                    }
                    if (config.Langs != null)
                    {
                        langs = config.Langs.ToArray();
                    }
                }
                catch
                {

                }
            }

            var projectId = solution.Projects.First(p =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return p.FilePath == project.FullName;
            }).Id;
            var compilationProject = solution.GetProject(projectId);
            var resourceKeys = StringLocalizerHelper.FindResourceKeys(compilationProject);
            StringLocalizerHelper.GenResourceFiles(resourceKeys, langs, compilationProject.DefaultNamespace, projectFullPath, resourcesPath, isFileStyle);
        }
    }
    internal class ResxGenConfig
    {
        public List<string> Langs { get; set; }
        public bool? IsFileStyle { get; set; }
        public string ResourcesPath { get; set; }
    }
}
