using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PS7ScriptDesk.Application.Diagnostics;

namespace PS7ScriptDesk.Shell.Diagnostics
{
    internal static class DisabledToolbarTooltipForensicLogger
    {
        private const string LogFileName = "DISABLED_TOOLBAR_TOOLTIP_RUNTIME_FORENSIC.log";
        private static readonly object SyncRoot = new();
        private static string? _logPath;
        private static bool _attached;

        public static void Attach(MainWindow window)
        {
            if (_attached || !DeveloperDiagnostics.IsEnabled)
            {
                return;
            }

            _attached = true;
            _logPath = ResolveLogPath();
            Write("TOOLTIP_FORENSIC_START", new Dictionary<string, object?>
            {
                ["processId"] = Environment.ProcessId,
                ["processPath"] = Environment.ProcessPath,
                ["baseDirectory"] = AppContext.BaseDirectory,
                ["logPath"] = _logPath,
                ["developerDiagnosticsEnabled"] = DeveloperDiagnostics.IsEnabled
            });
            LogPackageIdentity(window);
            CaptureStage(window, "AFTER_INITIALIZE_COMPONENT");
            window.Loaded += Window_Loaded;
            window.Closed += Window_Closed;
        }

        public static void CaptureStage(MainWindow window, string stage)
        {
            if (!_attached || !DeveloperDiagnostics.IsEnabled)
            {
                return;
            }

            Write("TOOLTIP_BINDING_STAGE", new Dictionary<string, object?> { ["stage"] = stage });
            foreach (var pair in new (string Name, FrameworkElement? Element)[]
            {
                ("RunTooltipHost", window.FindName("RunTooltipHost") as FrameworkElement),
                ("RunButton", window.FindName("RunButton") as FrameworkElement),
                ("RunSelectionTooltipHost", window.FindName("RunSelectionTooltipHost") as FrameworkElement),
                ("RunSelectionButton", window.FindName("RunSelectionButton") as FrameworkElement)
            })
            {
                LogElementState("TOOLTIP_BINDING_STATE", $"{stage}:{pair.Name}", pair.Element);
            }
        }

        private static void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not MainWindow window)
            {
                return;
            }

            var elements = new (string Name, FrameworkElement? Element)[]
            {
                ("MainWindow", window),
                ("MainToolbar", window.FindName("MainToolbar") as FrameworkElement),
                ("RunTooltipHost", window.FindName("RunTooltipHost") as FrameworkElement),
                ("RunButton", window.FindName("RunButton") as FrameworkElement),
                ("RunSelectionTooltipHost", window.FindName("RunSelectionTooltipHost") as FrameworkElement),
                ("RunSelectionButton", window.FindName("RunSelectionButton") as FrameworkElement),
                ("ContinueTooltipHost", window.FindName("ContinueTooltipHost") as FrameworkElement),
                ("ContinueButton", window.FindName("ContinueButton") as FrameworkElement)
            };

            foreach (var pair in elements)
            {
                if (pair.Element is null)
                {
                    Write("TOOLTIP_RUNTIME_ELEMENT_MISSING", new Dictionary<string, object?> { ["requestedName"] = pair.Name });
                    continue;
                }

                AttachElement(pair.Name, pair.Element);
                LogElementState("TOOLTIP_ELEMENT_STATE", pair.Name, pair.Element);
            }

            foreach (var pair in elements.Where(static pair => pair.Element is not null))
            {
                if (pair.Element is Control control)
                {
                    control.ApplyTemplate();
                    LogVisualDescendants(pair.Name, control);
                }
            }

            if (System.Windows.Application.Current?.TryFindResource("IdeToolbarTooltipHostStyle") is not null)
            {
                Write("TOOLTIP_RUNTIME_RESOURCE_FOUND", new Dictionary<string, object?>
                {
                    ["resourceKey"] = "IdeToolbarTooltipHostStyle",
                    ["resourceType"] = System.Windows.Application.Current?.TryFindResource("IdeToolbarTooltipHostStyle")?.GetType().FullName
                });
            }
            else
            {
                Write("TOOLTIP_RUNTIME_RESOURCE_MISSING", new Dictionary<string, object?> { ["resourceKey"] = "IdeToolbarTooltipHostStyle" });
            }

            Write("TOOLTIP_RUNTIME_HOST_FOUND", new Dictionary<string, object?>
            {
                ["runHost"] = window.FindName("RunTooltipHost") is not null,
                ["runSelectionHost"] = window.FindName("RunSelectionTooltipHost") is not null,
                ["continueHost"] = window.FindName("ContinueTooltipHost") is not null
            });
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => CaptureStage(window, "AFTER_LOADED_TEMPLATES")));
        }

        private static void AttachElement(string label, FrameworkElement element)
        {
            element.AddHandler(UIElement.MouseEnterEvent, new MouseEventHandler((s, e) => LogMouse(label, "MouseEnter", s, e)), true);
            element.AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler((s, e) => LogMouse(label, "MouseLeave", s, e)), true);
            element.AddHandler(UIElement.MouseMoveEvent, new MouseEventHandler((s, e) =>
            {
                LogMouse(label, "MouseMove", s, e);
                LogHitTest(label, element, e.GetPosition(element));
            }), true);
            element.AddHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler((s, e) => LogMouse(label, "PreviewMouseMove", s, e)), true);
            element.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((s, e) => LogMouse(label, "PreviewMouseDown", s, e)), true);
            element.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler((s, e) => LogMouse(label, "MouseDown", s, e)), true);
            element.AddHandler(ToolTipService.ToolTipOpeningEvent, new RoutedEventHandler((s, e) => LogTooltip(label, "TOOLTIP_OPENING", s)), true);
            element.AddHandler(ToolTipService.ToolTipClosingEvent, new RoutedEventHandler((s, e) => LogTooltip(label, "TOOLTIP_CLOSING", s)), true);
        }

        private static void LogMouse(string label, string eventName, object? sender, RoutedEventArgs e)
        {
            var element = sender as DependencyObject;
            var original = e.OriginalSource as DependencyObject;
            Write("TOOLTIP_MOUSE_EVENT", new Dictionary<string, object?>
            {
                ["label"] = label,
                ["event"] = eventName,
                ["source"] = Describe(sender as DependencyObject),
                ["originalSource"] = Describe(original),
                ["routedSource"] = Describe(e.Source as DependencyObject),
                ["elementIsEnabled"] = (element as UIElement)?.IsEnabled,
                ["elementIsMouseOver"] = (element as UIElement)?.IsMouseOver,
                ["elementIsMouseDirectlyOver"] = (element as UIElement)?.IsMouseDirectlyOver,
                ["timestamp"] = Environment.TickCount64
            });
        }

        private static void LogTooltip(string label, string eventName, object? sender)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            var value = element.ToolTip;
            Write(eventName, new Dictionary<string, object?>
            {
                ["label"] = label,
                ["element"] = Describe(element),
                ["effectiveToolTipType"] = value?.GetType().FullName,
                ["effectiveToolTipText"] = value is string text ? DeveloperDiagnostics.SanitizePreview(text, 180) : null,
                ["showOnDisabled"] = ToolTipService.GetShowOnDisabled(element),
                ["toolTipServiceEnabled"] = ToolTipService.GetIsEnabled(element),
                ["isEnabled"] = element.IsEnabled,
                ["isMouseOver"] = element.IsMouseOver,
                ["timestamp"] = Environment.TickCount64
            });
        }

        private static void LogHitTest(string label, FrameworkElement element, Point point)
        {
            var hit = VisualTreeHelper.HitTest(element, point)?.VisualHit;
            var ancestry = new List<string>();
            var current = hit;
            while (current is not null && ancestry.Count < 16)
            {
                ancestry.Add(Describe(current));
                current = VisualTreeHelper.GetParent(current);
            }

            Write("TOOLTIP_HIT_TEST", new Dictionary<string, object?>
            {
                ["label"] = label,
                ["point"] = $"{point.X:0.##},{point.Y:0.##}",
                ["topHitVisual"] = Describe(hit),
                ["visualAncestry"] = string.Join(" > ", ancestry),
                ["nearestHost"] = ancestry.FirstOrDefault(static value => value.Contains("Grid", StringComparison.Ordinal)),
                ["nearestButton"] = ancestry.FirstOrDefault(static value => value.Contains("Button", StringComparison.Ordinal)),
                ["timestamp"] = Environment.TickCount64
            });
        }

        private static void LogVisualDescendants(string label, Control root)
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);
            var count = 0;
            while (queue.Count > 0 && count++ < 40)
            {
                var current = queue.Dequeue();
                LogElementState("TOOLTIP_TEMPLATE_ELEMENT", $"{label}:{count}", current as FrameworkElement);
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                {
                    queue.Enqueue(VisualTreeHelper.GetChild(current, i));
                }
            }
        }

        private static void LogElementState(string eventName, string label, FrameworkElement? element)
        {
            if (element is null)
            {
                return;
            }

            var source = DependencyPropertyHelper.GetValueSource(element, FrameworkElement.ToolTipProperty);
            var binding = BindingOperations.GetBinding(element, FrameworkElement.ToolTipProperty);
            var bindingExpression = BindingOperations.GetBindingExpressionBase(element, FrameworkElement.ToolTipProperty);
            var localValue = element.ReadLocalValue(FrameworkElement.ToolTipProperty);
            var parent = element is Visual visual && VisualTreeHelper.GetParent(visual) is DependencyObject visualParent ? visualParent : null;
            var logicalParent = LogicalTreeHelper.GetParent(element);
            Write(eventName, new Dictionary<string, object?>
            {
                ["label"] = label,
                ["type"] = element.GetType().FullName,
                ["name"] = element.Name,
                ["isEnabled"] = element.IsEnabled,
                ["isHitTestVisible"] = element.IsHitTestVisible,
                ["visibility"] = element.Visibility.ToString(),
                ["opacity"] = element.Opacity,
                ["background"] = (element as Control)?.Background?.ToString(),
                ["toolTipType"] = element.ToolTip?.GetType().FullName,
                ["toolTipText"] = element.ToolTip is string text ? DeveloperDiagnostics.SanitizePreview(text, 180) : null,
                ["showOnDisabled"] = ToolTipService.GetShowOnDisabled(element),
                ["toolTipServiceEnabled"] = ToolTipService.GetIsEnabled(element),
                ["initialShowDelay"] = ToolTipService.GetInitialShowDelay(element),
                ["betweenShowDelay"] = ToolTipService.GetBetweenShowDelay(element),
                ["showDuration"] = ToolTipService.GetShowDuration(element),
                ["dataContextType"] = element.DataContext?.GetType().FullName,
                ["parent"] = Describe(parent),
                ["logicalParent"] = Describe(logicalParent),
                ["visualParent"] = Describe(parent),
                ["toolTipValueSource"] = source.BaseValueSource.ToString(),
                ["frameworkElementToolTipOwner"] = FrameworkElement.ToolTipProperty.OwnerType.FullName,
                ["toolTipServiceToolTipOwner"] = ToolTipService.ToolTipProperty.OwnerType.FullName,
                ["frameworkAndServiceToolTipDpSame"] = ReferenceEquals(FrameworkElement.ToolTipProperty, ToolTipService.ToolTipProperty),
                ["toolTipIsExpression"] = source.IsExpression,
                ["toolTipIsCoerced"] = source.IsCoerced,
                ["localValueType"] = localValue == DependencyProperty.UnsetValue ? "Unset" : localValue?.GetType().FullName,
                ["bindingExists"] = binding is not null,
                ["bindingPath"] = binding?.Path?.Path,
                ["bindingSourceType"] = binding?.Source?.GetType().FullName,
                ["bindingStatus"] = (bindingExpression as BindingExpression)?.Status.ToString(),
                ["bindingHasError"] = (bindingExpression as BindingExpression)?.HasError,
                ["timestamp"] = Environment.TickCount64
            });
        }

        private static void LogPackageIdentity(MainWindow window)
        {
            var shell = typeof(MainWindow).Assembly;
            var ui = typeof(PS7ScriptDesk.UI.ViewModels.MainWindowViewModel).Assembly;
            Write("TOOLTIP_PACKAGE_IDENTITY", new Dictionary<string, object?>
            {
                ["processPath"] = Environment.ProcessPath,
                ["shellAssemblyPath"] = shell.Location,
                ["shellFileVersion"] = shell.GetName().Version?.ToString(),
                ["shellTimestampUtc"] = File.Exists(shell.Location) ? File.GetLastWriteTimeUtc(shell.Location).ToString("O") : null,
                ["shellSha256"] = ComputeFileHash(shell.Location),
                ["uiAssemblyPath"] = ui.Location,
                ["uiAssemblyTimestampUtc"] = File.Exists(ui.Location) ? File.GetLastWriteTimeUtc(ui.Location).ToString("O") : null,
                ["uiAssemblySha256"] = ComputeFileHash(ui.Location),
                ["packageVersion"] = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                ["hostStyleResourcePresent"] = System.Windows.Application.Current?.TryFindResource("IdeToolbarTooltipHostStyle") is not null,
                ["mainWindowLoaded"] = window.IsLoaded
            });
        }

        private static string? ResolveLogPath()
        {
            var configured = Environment.GetEnvironmentVariable("PS7SCRIPTDESK_TOOLTIP_FORENSIC_LOG");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory is not null)
                {
                    var candidate = Path.Combine(directory.FullName, "docs", "LocalOnly_NotForGitHub", "Codex_Work");
                    if (Directory.Exists(candidate))
                    {
                        return Path.Combine(candidate, LogFileName);
                    }
                    directory = directory.Parent;
                }
            }

            return Path.Combine(DeveloperDiagnostics.DeveloperDebuggingRootDirectory, LogFileName);
        }

        private static string? ComputeFileHash(string path)
        {
            try
            {
                return File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string Describe(DependencyObject? value)
            => value is FrameworkElement element
                ? $"{element.GetType().Name}({element.Name})"
                : value?.GetType().Name ?? "<null>";

        private static void Write(string eventName, IReadOnlyDictionary<string, object?> properties)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_logPath))
                {
                    return;
                }

                var line = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(' ')
                    .Append(eventName);
                foreach (var property in properties)
                {
                    line.Append(' ').Append(property.Key).Append('=').Append(Format(property.Value));
                }

                lock (SyncRoot)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                    File.AppendAllText(_logPath, line.AppendLine().ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Forensic logging must never affect the application.
            }
        }

        private static string Format(object? value)
        {
            if (value is null)
            {
                return "<null>";
            }

            return DeveloperDiagnostics.SanitizePreview(value.ToString(), 300).Replace(' ', '_');
        }

        private static void Window_Closed(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.Loaded -= Window_Loaded;
                window.Closed -= Window_Closed;
            }
            Write("TOOLTIP_FORENSIC_END", new Dictionary<string, object?> { ["processId"] = Environment.ProcessId });
        }
    }
}
