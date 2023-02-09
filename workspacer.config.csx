#r "C:\Program Files\workspacer\workspacer.Shared.dll"
#r "C:\Program Files\workspacer\plugins\workspacer.Bar\workspacer.Bar.dll"
#r "C:\Program Files\workspacer\plugins\workspacer.Gap\workspacer.Gap.dll"
#r "C:\Program Files\workspacer\plugins\workspacer.ActionMenu\workspacer.ActionMenu.dll"
#r "C:\Program Files\workspacer\plugins\workspacer.FocusIndicator\workspacer.FocusIndicator.dll"

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using workspacer;
using workspacer.Bar;
using workspacer.Bar.Widgets;
using workspacer.Gap;
using workspacer.ActionMenu;
using workspacer.FocusIndicator;

return new Action<IConfigContext>((IConfigContext context) =>
{
	/* Variables */
	var fontSize = 9;
	var barHeight = 19;
	var fontName = "Cascadia Code PL";
	var background = new Color(0x0, 0x0, 0x0);
	var wsHasFocuscolor = new Color(0xEE, 0x82, 0xEE);
	var wsEmptyColor = new Color(0x95, 0x95, 0x95);
	var wsIndicatingBackColor = Color.Teal;
	var wsBlinkPeriod = 1000;
	//var transparencykey = new Color(0x0, 0xF, 0x0);
	//var istransparent = false;
	string[] wsNames = { "1: 🏠", "2: 🌎", "3: 📃", "4: 🌸" };
	/* If true only programs in allowedFileNames are managed if false all programs are managed except programs in disallowedFileNames */
	var useAllowedList = true;
	/* only used if useAllowedList is true (Names should be lowercase) */
	string[] allowedFileNames =
	{
		"te64", "brave", "vivaldi", "explorer++", "firefox", "librewolf", "notepad++",
		"sublime_text", "tixati", "modorganizer", "notepad", "vortex",
	};
	/* only used if useAllowedList is false (Names should be lowercase) */
	string[] disallowedFileNames =
	{
		"vlc", "steam", "calc1", "pinentry", "skyrimse", "conemu64", "steamwebhelper",
		"mpc-hc64", "explorer", "1Password", "bitwarden", "genshinimpact", "windowspy",
	};
	/* Lowercase */
	string[] disallowedTitles =
	{
		"save as", "moving...", "copying...", "validating nexus connection", "login failed",
	};
	string[] disallowedClasses =
	{
		"ShellTrayWnd", "MozillaDialogClass", "OperationStatusWindow",
	};
	/* Names of processes that should be routed to each workspace */
	string[][] processNames =
	{
		new string[] { "TE64", "Explorer++", },
		new string[] { "brave", "vivaldi", "firefox", "librewolf", },
		new string[] { "notepad++", "sublime_text", },
		new string[] { "tixati", "ModOrganizer", "Vortex", },
	};

	/* Config */
	context.CanMinimizeWindows = false;

	/* Gap */
	var gap = barHeight - 10;
	var gapPlugin = context.AddGap(new GapPluginConfig() { InnerGap = gap, OuterGap = gap / 2, Delta = gap / 2 });

	/* Bar */
	context.AddBar(new BarPluginConfig()
	{
		FontSize = fontSize,
		BarHeight = barHeight,
		FontName = fontName,
		DefaultWidgetBackground = background,
		//IsTransparent = istransparent,
		//TransparencyKey = transparencykey,
		LeftWidgets = () => new IBarWidget[]
		{
			new WorkspaceWidget() {
				WorkspaceHasFocusColor = wsHasFocuscolor,
				WorkspaceEmptyColor = wsEmptyColor,
				WorkspaceIndicatingBackColor = wsIndicatingBackColor,
				BlinkPeriod = wsBlinkPeriod,
			}, 
			new TextWidget(": "), 
			new TitleWidget() {
				IsShortTitle = true
			},
		},
		RightWidgets = () => new IBarWidget[]
		{
			//new BatteryWidget(),
			//new CpuPerformanceWidget(),
			//new MemoryPerformanceWidget(),
			//new NetworkPerformanceWidget(),
			new TextWidget(" |"),
			new TimeWidget(1000, "ddd dd-MMM-yyyy & HH:mm:ss"),
			new TextWidget("| "),
			new ActiveLayoutWidget(),
		}
	});

	/* Bar focus indicator */
	context.AddFocusIndicator();

	/* Default layouts */
	Func<ILayoutEngine[]> defaultLayouts = () => new ILayoutEngine[]
	{
		new TallLayoutEngine(),
		//new DwindleLayoutEngine(),
		new VertLayoutEngine(),
		new HorzLayoutEngine(),
		new FullLayoutEngine(),
	};

	context.DefaultLayouts = defaultLayouts;

	/* Workspaces */
	// Array of workspace names and their layouts
	(string, ILayoutEngine[])[] workspaces =
	{
		(wsNames[0], defaultLayouts()),
		(wsNames[1], defaultLayouts()),
		(wsNames[2], defaultLayouts()),
		(wsNames[3], defaultLayouts()),
	};

	foreach ((string name, ILayoutEngine[] layouts) in workspaces)
	{
		context.WorkspaceContainer.CreateWorkspace(name, layouts);
	}

	/* Filters */
	context.WindowRouter.AddFilter((window) => !disallowedClasses.Contains(window.Class));
	context.WindowRouter.AddFilter((window) => !disallowedTitles.Contains(window.Title.ToLower()));
	if (useAllowedList)
	{
		context.WindowRouter.AddFilter((window) => allowedFileNames.Contains(Path.GetFileNameWithoutExtension(window.ProcessFileName.ToLower())));
	}
	else
	{
		context.WindowRouter.AddFilter((window) => !disallowedFileNames.Contains(Path.GetFileNameWithoutExtension(window.ProcessFileName.ToLower())));
	}

	/* Routes */
	for (int i = 0; i < processNames.Length; i++)
	{
		for (int j = 0; j < processNames[i].Length; j++)
		{
			context.WindowRouter.RouteProcessName(processNames[i][j], wsNames[i]);
		}
	}

	/* Action menu */
	var actionMenu = context.AddActionMenu(new ActionMenuPluginConfig()
	{
		RegisterKeybind = false,
		MenuHeight = barHeight,
		FontSize = fontSize,
		FontName = fontName,
		Background = background,
	});

	/* Action menu builder */
	Func<ActionMenuItemBuilder> createActionMenuBuilder = () =>
	{
		var menuBuilder = actionMenu.Create();

		// Switch to workspace
		menuBuilder.AddMenu("switch", () =>
		{
			var workspaceMenu = actionMenu.Create();
			var monitor = context.MonitorContainer.FocusedMonitor;
			var workspaces = context.WorkspaceContainer.GetWorkspaces(monitor);

			Func<int, Action> createChildMenu = (workspaceIndex) => () =>
			{
				context.Workspaces.SwitchMonitorToWorkspace(monitor.Index, workspaceIndex);
			};

			int workspaceIndex = 0;

			foreach (var workspace in workspaces)
			{
				workspaceMenu.Add(workspace.Name, createChildMenu(workspaceIndex));
				workspaceIndex++;
			}

			return workspaceMenu;
		});

		// Move window to workspace
		menuBuilder.AddMenu("move", () =>
		{
			var moveMenu = actionMenu.Create();
			var focusedWorkspace = context.Workspaces.FocusedWorkspace;

			var workspaces = context.WorkspaceContainer.GetWorkspaces(focusedWorkspace).ToArray();
			Func<int, Action> createChildMenu = (index) => () => { context.Workspaces.MoveFocusedWindowToWorkspace(index); };

			for (int i = 0; i < workspaces.Length; i++)
			{
				moveMenu.Add(workspaces[i].Name, createChildMenu(i));
			}

			return moveMenu;
		});

		// Rename workspace
		menuBuilder.AddFreeForm("rename", (name) =>
		{
			context.Workspaces.FocusedWorkspace.Name = name;
		});

		// Create workspace
		menuBuilder.AddFreeForm("create workspace", (name) =>
		{
			context.WorkspaceContainer.CreateWorkspace(name);
		});

		// Delete focused workspace
		menuBuilder.Add("close", () =>
		{
			context.WorkspaceContainer.RemoveWorkspace(context.Workspaces.FocusedWorkspace);
		});

		// Workspacer
		menuBuilder.Add("toggle keybind helper", () => context.Keybinds.ShowKeybindDialog());
		menuBuilder.Add("toggle enabled", () => context.Enabled = !context.Enabled);
		menuBuilder.Add("restart", () => context.Restart());
		menuBuilder.Add("quit", () => context.Quit());

		return menuBuilder;
	};

	var actionMenuBuilder = createActionMenuBuilder();

	/* Keybindings */
	Action setKeybindings = () =>
	{
		KeyModifiers winShift = KeyModifiers.Win | KeyModifiers.Shift;
		KeyModifiers winCtrl = KeyModifiers.Win | KeyModifiers.Control;
		KeyModifiers win = KeyModifiers.Win;

		IKeybindManager manager = context.Keybinds;

		var workspaces = context.Workspaces;

		manager.UnsubscribeAll();
		manager.Subscribe(MouseEvent.LButtonDown, () => workspaces.SwitchFocusedMonitorToMouseLocation());

		// Left, Right keys
		manager.Subscribe(winCtrl, Keys.Left, () => workspaces.SwitchToPreviousWorkspace(), "switch to previous workspace");
		manager.Subscribe(winCtrl, Keys.Right, () => workspaces.SwitchToNextWorkspace(), "switch to next workspace");

		manager.Subscribe(winShift, Keys.Left, () => workspaces.MoveFocusedWindowToPreviousMonitor(), "move focused window to previous monitor");
		manager.Subscribe(winShift, Keys.Right, () => workspaces.MoveFocusedWindowToNextMonitor(), "move focused window to next monitor");

		// H, L keys
		manager.Subscribe(winShift, Keys.H, () => workspaces.FocusedWorkspace.ShrinkPrimaryArea(), "shrink primary area");
		manager.Subscribe(winShift, Keys.L, () => workspaces.FocusedWorkspace.ExpandPrimaryArea(), "expand primary area");

		manager.Subscribe(winCtrl, Keys.H, () => workspaces.FocusedWorkspace.DecrementNumberOfPrimaryWindows(), "decrement number of primary windows");
		manager.Subscribe(winCtrl, Keys.L, () => workspaces.FocusedWorkspace.IncrementNumberOfPrimaryWindows(), "increment number of primary windows");

		// K, J keys
		manager.Subscribe(winShift, Keys.K, () => workspaces.FocusedWorkspace.SwapFocusAndNextWindow(), "swap focus and next window");
		manager.Subscribe(winShift, Keys.J, () => workspaces.FocusedWorkspace.SwapFocusAndPreviousWindow(), "swap focus and previous window");

		manager.Subscribe(winCtrl, Keys.K, () => workspaces.FocusedWorkspace.FocusNextWindow(), "focus next window");
		manager.Subscribe(winCtrl, Keys.J, () => workspaces.FocusedWorkspace.FocusPreviousWindow(), "focus previous window");

		// Add, Subtract keys
		manager.Subscribe(winCtrl, Keys.Add, () => gapPlugin.IncrementInnerGap(), "increment inner gap");
		manager.Subscribe(winCtrl, Keys.Subtract, () => gapPlugin.DecrementInnerGap(), "decrement inner gap");

		manager.Subscribe(winShift, Keys.Add, () => gapPlugin.IncrementOuterGap(), "increment outer gap");
		manager.Subscribe(winShift, Keys.Subtract, () => gapPlugin.DecrementOuterGap(), "decrement outer gap");

		// Other shortcuts
		manager.Subscribe(winCtrl, Keys.N, () => context.Workspaces.FocusedWorkspace.ResetLayout(), "reset layout");
		manager.Subscribe(winCtrl, Keys.P, () => actionMenu.ShowMenu(actionMenuBuilder), "show menu");
		manager.Subscribe(winShift, Keys.Escape, () => context.Enabled = !context.Enabled, "toggle enabled/disabled");
		manager.Subscribe(winShift, Keys.I, () => context.ToggleConsoleWindow(), "toggle console window");
		manager.Subscribe(winShift, Keys.Enter, () => context.Workspaces.FocusedWorkspace.SwapFocusAndPrimaryWindow(), "swap focus and primary window");
		manager.Subscribe(winCtrl, Keys.Space, () => context.Workspaces.FocusedWorkspace.NextLayoutEngine(), "next layout");
		manager.Subscribe(winShift, Keys.Space, () => context.Workspaces.FocusedWorkspace.PreviousLayoutEngine(), "previous layout");
		manager.Subscribe(win, Keys.D1, () => context.Workspaces.SwitchToWorkspace(0), "switch to workspace 1");
		manager.Subscribe(win, Keys.D2, () => context.Workspaces.SwitchToWorkspace(1), "switch to workspace 2");
		manager.Subscribe(win, Keys.D3, () => context.Workspaces.SwitchToWorkspace(2), "switch to workspace 3");
		manager.Subscribe(win, Keys.D4, () => context.Workspaces.SwitchToWorkspace(3), "switch to workspace 4");
		manager.Subscribe(win, Keys.D5, () => context.Workspaces.SwitchToWorkspace(4), "switch to workspace 5");
		manager.Subscribe(win, Keys.D6, () => context.Workspaces.SwitchToWorkspace(5), "switch to workspace 6");
		manager.Subscribe(win, Keys.D7, () => context.Workspaces.SwitchToWorkspace(6), "switch to workspace 7");
		manager.Subscribe(win, Keys.D8, () => context.Workspaces.SwitchToWorkspace(7), "switch to workspace 8");
		manager.Subscribe(win, Keys.D9, () => context.Workspaces.SwitchToWorkspace(8), "switch to workspace 9");
		manager.Subscribe(win, Keys.D0, () => context.Workspaces.SwitchToWorkspace(9), "switch to workspace 10");
		manager.Subscribe(winShift, Keys.D1, () => context.Workspaces.MoveFocusedWindowToWorkspace(0), "switch focused window to workspace 1");
		manager.Subscribe(winShift, Keys.D2, () => context.Workspaces.MoveFocusedWindowToWorkspace(1), "switch focused window to workspace 2");
		manager.Subscribe(winShift, Keys.D3, () => context.Workspaces.MoveFocusedWindowToWorkspace(2), "switch focused window to workspace 3");
		manager.Subscribe(winShift, Keys.D4, () => context.Workspaces.MoveFocusedWindowToWorkspace(3), "switch focused window to workspace 4");
		manager.Subscribe(winShift, Keys.D5, () => context.Workspaces.MoveFocusedWindowToWorkspace(4), "switch focused window to workspace 5");
		manager.Subscribe(winShift, Keys.D6, () => context.Workspaces.MoveFocusedWindowToWorkspace(5), "switch focused window to workspace 6");
		manager.Subscribe(winShift, Keys.D7, () => context.Workspaces.MoveFocusedWindowToWorkspace(6), "switch focused window to workspace 7");
		manager.Subscribe(winShift, Keys.D8, () => context.Workspaces.MoveFocusedWindowToWorkspace(7), "switch focused window to workspace 8");
		manager.Subscribe(winShift, Keys.D9, () => context.Workspaces.MoveFocusedWindowToWorkspace(8), "switch focused window to workspace 9");
		manager.Subscribe(winShift, Keys.D0, () => context.Workspaces.MoveFocusedWindowToWorkspace(9), "switch focused window to workspace 10");
		manager.Subscribe(winCtrl, Keys.X, () => Process.Start("C:\\Program Files\\AutoHotkey\\WindowSpy.exe"), "start \"Window Spy\"");

	};
	setKeybindings();
});
