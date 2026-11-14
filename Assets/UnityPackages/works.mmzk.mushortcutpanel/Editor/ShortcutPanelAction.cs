using System.Collections.Generic;

namespace Mmzkworks.muShortcutPanel.Editor
{
    /// <summary>Rules for tracing an action's steps by dragging from its knob over other items.</summary>
    internal static class ShortcutPanelActionTrace
    {
        /// <summary>
        /// Appends <paramref name="hit"/> to <paramref name="steps"/> when it can be a step of <paramref name="action"/>.
        /// Labels, the action itself and items already in the list are ignored.
        /// </summary>
        /// <returns>True when the item was added.</returns>
        public static bool TryAdd(List<string> steps, ShortcutPanelItem action, ShortcutPanelItem hit)
        {
            if (steps == null || hit == null || string.IsNullOrEmpty(hit.id)) return false;
            if (hit.kind == ShortcutItemKind.Label || hit.kind == ShortcutItemKind.Image) return false;
            if (action != null && hit.id == action.id) return false;
            if (steps.Contains(hit.id)) return false;
            steps.Add(hit.id);
            return true;
        }
    }

    /// <summary>Turns an action into the flat list of items to run.</summary>
    internal static class ShortcutPanelActionPlan
    {
        /// <summary>
        /// Expands the steps of the action <paramref name="actionId"/> in order. Nested actions are expanded in place,
        /// labels are skipped. Returns null and sets <paramref name="error"/> for a missing item or a cycle.
        /// </summary>
        public static List<ShortcutPanelItem> Expand(ShortcutPanelDocument doc, string actionId, out string error)
        {
            error = null;
            var action = doc?.Find(actionId);
            if (action == null || action.kind != ShortcutItemKind.Action)
            {
                error = "Action not found";
                return null;
            }

            var result = new List<ShortcutPanelItem>();
            var stack = new List<string>();
            return ExpandInto(doc, action, stack, result, ref error) ? result : null;
        }

        private static bool ExpandInto(ShortcutPanelDocument doc, ShortcutPanelItem action, List<string> stack,
            List<ShortcutPanelItem> result, ref string error)
        {
            if (stack.Contains(action.id))
            {
                error = $"Action cycle: {Name(doc, stack[0])} -> … -> {Name(action)}";
                return false;
            }

            stack.Add(action.id);
            foreach (var id in action.steps ?? new List<string>())
            {
                var step = doc.Find(id);
                if (step == null)
                {
                    error = $"Missing step in {Name(action)}";
                    return false;
                }

                switch (step.kind)
                {
                    case ShortcutItemKind.Label:
                    case ShortcutItemKind.Image:
                        continue;
                    case ShortcutItemKind.Action:
                        if (!ExpandInto(doc, step, stack, result, ref error)) return false;
                        break;
                    default:
                        result.Add(step);
                        break;
                }
            }
            stack.RemoveAt(stack.Count - 1);
            return true;
        }

        private static string Name(ShortcutPanelDocument doc, string id) => Name(doc.Find(id));

        private static string Name(ShortcutPanelItem action) =>
            action == null ? "?" : string.IsNullOrEmpty(action.title) ? "Action" : action.title;
    }
}
