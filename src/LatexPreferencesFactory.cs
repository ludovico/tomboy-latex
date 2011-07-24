using System;
using Tomboy;

namespace Tomboy.Latex
{
    public class LatexPreferenceFactory : AddinPreferenceFactory
    {
        public override Gtk.Widget CreatePreferenceWidget ()
        {
            return new LatexPreferences ();
        }
    }
}
