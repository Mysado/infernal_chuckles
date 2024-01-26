using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sisus.Init.EditorOnly
{
    internal sealed class TypeDropdownButton : DropdownButton
    {
        public TypeDropdownButton
        (
            GUIContent prefixLabel, GUIContent buttonLabel,
            IEnumerable<Type> types, IEnumerable<Type> selectedItems,
            Action<Type> onSelectedItemChanged,
            string menuTitle = "Types",
            Func<Type, (string fullPath, Texture icon)> itemContentGetter = null
        )
         : base(prefixLabel, buttonLabel, (Rect belowRect) => TypeDropdownWindow.Show(belowRect, types, selectedItems, onSelectedItemChanged, menuTitle, itemContentGetter)) { }
    }
}