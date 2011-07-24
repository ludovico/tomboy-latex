using System;
using System.IO;
using Mono.Unix;

namespace Tomboy.Latex
{
    public class LatexPreferences : Gtk.VBox
    {
        Gtk.TextView text_header;
        Gtk.TextView text_footer;
        Gtk.CheckButton dollar_enabled_checkbutton;
        Gtk.Button reset_button;
        Gtk.Button apply_button;

        public LatexPreferences () : base (false, 12)
        {
            string header = LatexAddin.GetHeader();
            string footer = LatexAddin.GetFooter();
            bool dollar_enabled = LatexAddin.IsDollarEnabled();

            Gtk.Label label = new Gtk.Label (Catalog.GetString (
                                "Enter the LaTeX code used for rendering " +
                                    "the formulas.\n" +
                                "If you do not know what you are doing, just " +
                                    "use the default.\n" +
                                "Press \"Reset to defaults\" " +
                                    "(followed by \"Apply\") " +
                                    "if something went wrong."));
            label.Wrap = true;
            label.Xalign = 0;
            PackStart (label);

            Gtk.ScrolledWindow header_window = new Gtk.ScrolledWindow ();
            text_header = new Gtk.TextView ();
            text_header.Buffer.Text = header;
            text_header.Buffer.Changed += OnBufferChanged;
            header_window.AddWithViewport(text_header);
            PackStart (header_window);

            Gtk.Label label_f = new Gtk.Label (Catalog.GetString (
                                "-- code from note (without \"\\[\" " +
                                "and \"\\]\") inserted here --"));
            label_f.Wrap = true;
            label_f.Xalign = 0;
            PackStart (label_f);

            Gtk.ScrolledWindow footer_window = new Gtk.ScrolledWindow ();
            text_footer = new Gtk.TextView ();
            text_footer.Buffer.Text = footer;
            text_footer.Buffer.Changed += OnBufferChanged;
            footer_window.AddWithViewport(text_footer);
            PackStart (footer_window);

            Gtk.VBox vbox = new Gtk.VBox (true, 1);
            dollar_enabled_checkbutton = new Gtk.CheckButton ("also convert \"$...$\" formulas (restart required on change)");
            dollar_enabled_checkbutton.Active = dollar_enabled;
            dollar_enabled_checkbutton.Toggled += new EventHandler (OnDollarEnabledToggled);
            vbox.PackStart (dollar_enabled_checkbutton);
            PackStart (vbox);

            reset_button = new Gtk.Button ("Reset to defaults");
            if (LatexAddin.DEFAULT_HEADER == header &&
                    LatexAddin.DEFAULT_FOOTER == footer &&
                    LatexAddin.DEFAULT_DOLLAR_ENABLED == dollar_enabled)
            {
                reset_button.Sensitive = false;
            } else {
                reset_button.Sensitive = true;
            }
            reset_button.Clicked += OnResetClicked;

            apply_button = new Gtk.Button (Gtk.Stock.Apply);
            apply_button.Sensitive = false;
            apply_button.Clicked += OnApplyClicked;

            Gtk.HButtonBox hbutton_box = new Gtk.HButtonBox ();
            hbutton_box.Layout = Gtk.ButtonBoxStyle.Start;
            hbutton_box.Spacing = 6;

            hbutton_box.PackStart (reset_button);
            hbutton_box.PackStart (apply_button);
            PackStart (hbutton_box, false, false, 0);

            ShowAll ();
        }

        void OnBufferChanged (object sender, EventArgs args)
        {
            reset_button.Sensitive = true;
            apply_button.Sensitive = true;
        }

        void OnDollarEnabledToggled (object sender, EventArgs args)
        {
            reset_button.Sensitive = true;
            apply_button.Sensitive = true;
        }

        void OnResetClicked (object sender, EventArgs args)
        {
            text_header.Buffer.Text = LatexAddin.DEFAULT_HEADER;
            text_footer.Buffer.Text = LatexAddin.DEFAULT_FOOTER;
            dollar_enabled_checkbutton.Active = LatexAddin.DEFAULT_DOLLAR_ENABLED;
            reset_button.Sensitive = false;
        }

        void OnApplyClicked (object sender, EventArgs args)
        {
            LatexAddin.SetHeaderFooterAndDollarEnabled(text_header.Buffer.Text, text_footer.Buffer.Text, dollar_enabled_checkbutton.Active);
            apply_button.Sensitive = false;
        }


    }
}
