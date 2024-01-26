/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEngine;

namespace InfinityCode.RunDesiredScene
{
    public static class TempContent
    {
        private static GUIContent _text = new GUIContent();
        private static GUIContent _image = new GUIContent();

        public static GUIContent Get(GUIContent content)
        {
            if (content.image != null)
            {
                _image.image = content.image;
                _image.text = content.text;
                _image.tooltip = content.tooltip;
                return _image;
            }
            
            _text.text = content.text;
            _text.tooltip = content.tooltip;
            _text.image = null;
            return _text;
        }

        public static GUIContent Get(string text)
        {
            _text.text = text;
            _text.tooltip = null;
            _text.image = null;
            return _text;
        }

        public static GUIContent Get(string text, string tooltip)
        {
            _text.text = text;
            _text.tooltip = tooltip;
            _text.image = null;
            return _text;
        }

        public static GUIContent Get(Texture texture)
        {
            _image.image = texture;
            _image.tooltip = null;
            _image.text = null;
            return _image;
        }

        public static GUIContent Get(Texture texture, string tooltip)
        {
            _image.image = texture;
            _image.tooltip = tooltip;
            _image.text = null;
            return _image;
        }
    }
}