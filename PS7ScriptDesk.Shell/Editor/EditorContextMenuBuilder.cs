using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfMenuItem = System.Windows.Controls.MenuItem;

namespace PS7ScriptDesk.Shell.Editor;

public static class EditorContextMenuBuilder
{
    public static IReadOnlyList<EditorCommandDefinition> Populate(
        WpfMenuItem selectionMenu,
        WpfMenuItem transformMenu,
        WpfMenuItem otherMenu,
        EditorCommandRegistry registry,
        bool hasNonEmptySelection,
        Action<EditorCommandDefinition> execute)
    {
        ArgumentNullException.ThrowIfNull(selectionMenu);
        ArgumentNullException.ThrowIfNull(transformMenu);
        ArgumentNullException.ThrowIfNull(otherMenu);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(execute);

        selectionMenu.Items.Clear();
        transformMenu.Items.Clear();
        otherMenu.Items.Clear();
        var commands = EditorContextCommandProvider.GetAvailableCommands(registry, hasNonEmptySelection);
        var menus = new Dictionary<string, WpfMenuItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["Selection"] = selectionMenu,
            ["Transform"] = transformMenu,
            ["More"] = otherMenu
        };

        foreach (var menuGroup in commands
            .GroupBy(command => command.ContextGroup ?? "More", StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetGroupOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!menus.TryGetValue(menuGroup.Key, out var parentMenu))
            {
                parentMenu = otherMenu;
            }

            var groupedCommands = menuGroup
                .GroupBy(command => command.ContextSubgroup, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Min(command => command.ContextOrder))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var subgroup in groupedCommands)
            {
                if (subgroup.Key is not null)
                {
                    var submenu = new WpfMenuItem { Header = subgroup.Key };
                    foreach (var command in OrderCommands(subgroup))
                    {
                        submenu.Items.Add(CreateCommandMenuItem(command, execute));
                    }

                    if (submenu.Items.Count > 0)
                    {
                        AddSeparatorIfNeeded(parentMenu);
                        parentMenu.Items.Add(submenu);
                    }
                }
                else
                {
                    foreach (var command in OrderCommands(subgroup))
                    {
                        parentMenu.Items.Add(CreateCommandMenuItem(command, execute));
                    }
                }
            }
        }

        selectionMenu.Visibility = selectionMenu.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        transformMenu.Visibility = transformMenu.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        otherMenu.Visibility = otherMenu.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        return commands;
    }

    private static IEnumerable<EditorCommandDefinition> OrderCommands(IEnumerable<EditorCommandDefinition> commands) =>
        commands.OrderBy(command => command.ContextOrder)
            .ThenBy(command => command.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.Id, StringComparer.OrdinalIgnoreCase);

    private static WpfMenuItem CreateCommandMenuItem(EditorCommandDefinition command, Action<EditorCommandDefinition> execute)
    {
        var menuItem = new WpfMenuItem
        {
            Header = command.DisplayName,
            InputGestureText = command.ShortcutText,
            Tag = command
        };
        menuItem.Click += (_, _) => execute(command);
        return menuItem;
    }

    private static void AddSeparatorIfNeeded(WpfMenuItem menu)
    {
        if (menu.Items.Count > 0 && menu.Items[^1] is not Separator)
        {
            menu.Items.Add(new Separator());
        }
    }

    private static int GetGroupOrder(string group) => group switch
    {
        "Selection" => 0,
        "Transform" => 1,
        _ => 2
    };
}
