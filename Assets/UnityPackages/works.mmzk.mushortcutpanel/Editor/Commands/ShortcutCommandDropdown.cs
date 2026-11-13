using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Mmzkworks.muShortcutPanel.Editor
{
    /// <summary>Searchable picker of the built-in commands, grouped by category.</summary>
    internal sealed class ShortcutCommandDropdown : AdvancedDropdown
    {
        private readonly Action<string> _onPicked;

        private sealed class CommandItem : AdvancedDropdownItem
        {
            public readonly string CommandId;

            public CommandItem(ShortcutCommand command) : base(command.Name)
            {
                CommandId = command.Id;
            }
        }

        private ShortcutCommandDropdown(Action<string> onPicked) : base(new AdvancedDropdownState())
        {
            _onPicked = onPicked;
            minimumSize = new Vector2(260f, 360f);
        }

        /// <summary>Must be called from OnGUI. <paramref name="rect"/> is in GUI coordinates.</summary>
        public static void Show(Rect rect, Action<string> onPicked) => new ShortcutCommandDropdown(onPicked).Show(rect);

        protected override AdvancedDropdownItem BuildRoot()
        {
            var nextId = 1;
            var root = new AdvancedDropdownItem("Command") { id = nextId++ };
            var groups = new Dictionary<string, AdvancedDropdownItem>();
            foreach (var category in ShortcutCommandCategory.All)
            {
                var group = new AdvancedDropdownItem(category) { id = nextId++ };
                groups.Add(category, group);
                root.AddChild(group);
            }

            foreach (var command in ShortcutCommandRegistry.All)
            {
                var item = new CommandItem(command) { id = nextId++, icon = ShortcutCommandIcons.For(command) as Texture2D };
                if (groups.TryGetValue(command.Category, out var group)) group.AddChild(item);
                else root.AddChild(item);
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is CommandItem c) _onPicked?.Invoke(c.CommandId);
        }
    }
}
