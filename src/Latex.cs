/*
 * Latex.cs: A tomboy Addin that reders inline LaTeX math code.
 *
 * Copyright 2007,  Christian Reitwießner <christian@reitwiessner.de>
 *
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 *
 */

using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using Mono.Unix;

using Gtk;

using Tomboy;

namespace Tomboy.Latex
{

public class LatexImageRequest
{
    static Random random = new Random();
    static string[] latex_blacklist = {"\\def", "\\let", "\\futurelet",
        "\\newcommand", "\\renewcommand", "\\else", "\\fi", "\\write",
        "\\input", "\\include", "\\chardef", "\\catcode", "\\makeatletter",
        "\\noexpand", "\\toksdef", "\\every", "\\errhelp", "\\errorstopmode",
        "\\scrollmode", "\\nonstopmode", "\\batchmode", "\\read", "\\csname",
        "\\newhelp", "\\relax", "\\afterground", "\\afterassignment",
        "\\expandafter", "\\noexpand", "\\special", "\\command", "\\loop",
        "\\repeat", "\\toks", "\\output", "\\line", "\\mathcode", "\\name",
        "\\item", "\\section", "\\mbox", "\\DeclareRobustCommand",
        "\\[", "\\]", "$", "\\(", "\\)"};


    string code;
    LatexAddin requester;

    public string Code {
        get { return code; }
    }

    public LatexImageRequest (string code, LatexAddin requester)
    {
        this.code = code;
        this.requester = requester;
    }

    public void NotifyRequester ()
    {
        Gtk.Application.Invoke(delegate { requester.ImageGenerated (); } );
    }

    public override bool Equals (System.Object obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;
        LatexImageRequest r = (LatexImageRequest) obj;
        return (r.code.Equals(code) && r.requester == requester);
    }

    public override int GetHashCode ()
    {
        return code.GetHashCode() ^ requester.GetHashCode();
    }


    public string CreateImage ()
    {
        string realCode;
        if (code.StartsWith("\\")) {
            if (code.Length < 5) return null;
            realCode = code.Substring(2, code.Length - 4);
        } else {
            if (code.Length < 3) return null;
            realCode = code.Substring(1, code.Length - 2);
        }

        if (realCode.Trim().Equals(String.Empty)) {
            return null;
        }


        foreach (string s in latex_blacklist) {
            if (realCode.IndexOf(s) != -1) {
                return null;
            }
        }

        Logger.Info("Latex: Creating image for {0}...", code);



        string tmpfile = Path.Combine(Path.GetTempPath(), "tbltx_" + random.Next());

        try {
            System.IO.StreamWriter writer = new System.IO.StreamWriter(tmpfile + ".tex", false);
            writer.Write(LatexAddin.GetHeader());
            writer.Write(realCode);
            writer.Write(LatexAddin.GetFooter());
            writer.Close();


            Process p = new Process ();
            p.StartInfo.FileName = "latex";
            p.StartInfo.Arguments = "--interaction=nonstopmode \"" + tmpfile + ".tex\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.WorkingDirectory = Path.GetTempPath();
            p.Start ();
            p.WaitForExit();
            if (p.ExitCode != 0) return null;

            p = new Process ();
            p.StartInfo.FileName = "dvipng";
            p.StartInfo.Arguments = "-pp 1 -T tight -o \"" + tmpfile + ".png\" \"" + tmpfile + ".dvi\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.WorkingDirectory = Path.GetTempPath();
            p.Start ();
            p.WaitForExit();
            if (p.ExitCode != 0) return null;

            return tmpfile + ".png";
        } finally {
            string[] extensions = {".tex", ".log", ".aux", ".dvi"};
            foreach (string ext in extensions) {
                try {
                    File.Delete(tmpfile + ext);
                } catch {
                }
            }
        }
    }
}



public class LatexManager
{
    IDictionary<string, Gdk.Pixbuf> images;
    Queue<LatexImageRequest> requests;

    Thread queue_thread;


    public LatexManager()
    {
        images = new Dictionary<string, Gdk.Pixbuf> ();
        requests = new Queue<LatexImageRequest> ();

        queue_thread = new Thread(new ThreadStart(this.WorkOnQueue));
        queue_thread.Start();
    }

    public void ClearImageCache ()
    {
        images.Clear();
    }

    public Gdk.Pixbuf GetImage (string code, LatexAddin requester)
    {
        lock (queue_thread) {
            if (images.ContainsKey (code)) {
                return images[code];
            } else {
                LatexImageRequest req = new LatexImageRequest (code, requester);
                if (!requests.Contains (req)) {
                    requests.Enqueue (req);
                    Monitor.Pulse(queue_thread);
                }
                return null;
            }
        }
    }

    void WorkOnQueue ()
    {
        while (true) {
            LatexImageRequest request;
            lock (queue_thread) {
                if (requests.Count == 0) {
                    Monitor.Wait(queue_thread);
                    continue;
                }
                request = requests.Dequeue ();
            }
            if (images.ContainsKey (request.Code)) {
                request.NotifyRequester ();
            } else {
                string imageFile = request.CreateImage ();
                if (imageFile == null) {
                    continue;
                }

                Gtk.Application.Invoke (delegate { CreatePixbufAndNotify (request, imageFile); });
            }
        }

    }

    void CreatePixbufAndNotify (LatexImageRequest request, string imageFile)
    {
        try {
            Gdk.Pixbuf image = new Gdk.Pixbuf (imageFile);
            lock (queue_thread) {
                images[request.Code] = image;
            }
            request.NotifyRequester ();
        } finally {
            File.Delete(imageFile);
        }
    }

}


public class LatexImage
{
    string latex_code;
    LatexImageTag image_tag;
    Gtk.TextMark image_position;

    public Gtk.TextIter ImagePosition {
        get { return image_position.Buffer.GetIterAtMark(image_position); }
    }


    public LatexImage(Gtk.TextIter codeStart, Gtk.TextIter codeEnd, Gdk.Pixbuf image, LatexImageTag image_tag)
    {
        latex_code = codeStart.GetText(codeEnd);

        Gtk.TextBuffer buffer = codeStart.Buffer;

        Gtk.TextIter imageStart = codeStart;
        Gtk.TextIter imageEnd;

        buffer.InsertPixbuf(ref imageStart, image);
        imageStart.BackwardChar();
        image_position = buffer.CreateMark(null, imageStart, false);

        imageStart = image_position.Buffer.GetIterAtMark(image_position);
        imageEnd = imageStart;
        imageEnd.ForwardChar();

        image_tag.LatexImage = this;
        buffer.ApplyTag(image_tag, imageStart, imageEnd);
        this.image_tag = image_tag;

        codeStart = image_position.Buffer.GetIterAtMark(image_position);
        codeStart.ForwardChar();
        codeEnd = codeStart;
        codeEnd.ForwardChars(latex_code.Length);
        buffer.ApplyTag("latex_code", codeStart, codeEnd);
    }

    public bool IsImagePosition(Gtk.TextIter pos)
    {
        return image_position.Buffer.GetIterAtMark(image_position).Equal(pos);
    }

    public void Open ()
    {
        Gtk.TextIter start, end;
        start = image_position.Buffer.GetIterAtMark(image_position);
        end = start;
        end.ForwardChar();
        image_position.Buffer.RemoveTag(image_tag, start, end);
        image_position.Buffer.Delete(ref start, ref end);

        start = image_position.Buffer.GetIterAtMark(image_position);
        end = start;
        end.ForwardChars(latex_code.Length);
        image_position.Buffer.RemoveTag("latex_code", start, end);

        image_position.Buffer.DeleteMark(image_position);
    }
}


/* TODO: this tag and the pixbuf must not be copied to the clipboard */
public class LatexImageTag : DynamicNoteTag
{
    public LatexAddin Addin;
    public LatexImage LatexImage;

    public LatexImageTag() : base()
    {
    }

    public override void Initialize (string element_name)
    {
        base.Initialize (element_name);

        CanSerialize = false;
        CanActivate = true;
        Editable = false;
    }

    protected override bool OnActivate (NoteEditor editor,
                                        Gtk.TextIter start,
                                        Gtk.TextIter end)
    {
        Addin.OpenLatexImage(LatexImage);
        return true;
    }
}


public class LatexAddin : NoteAddin 
{
    public const string PREFERENCES_KEY_HEADER =
                                        "/apps/tomboy/latex_math/header";
    public const string PREFERENCES_KEY_FOOTER =
                                        "/apps/tomboy/latex_math/footer";
    public const string PREFERENCES_KEY_DOLLAR_ENABLED =
                                        "/apps/tomboy/latex_math/dollar_enabled";
    public const string DEFAULT_HEADER = "\\documentclass[12pt]{article}\n" +
                    "\\usepackage[dvips]{graphicx}\n\\usepackage{amsmath}\n" +
                    "\\usepackage{amssymb}\n\\pagestyle{empty}\n" +
                    "\\begin{document}\n\\begin{gather*}\n";
    public const string DEFAULT_FOOTER = "\\end{gather*}\n\\end{document}";
    public const bool DEFAULT_DOLLAR_ENABLED = false;
    static LatexManager manager;
    static LatexAddin () {
        manager = new LatexManager ();
    }

    List<LatexImage> images;

    bool checkLatex_running; 

    public string Title { get { return Note.Title; } }


    public LatexAddin ()
    {
        checkLatex_running = false;
        images = new List<LatexImage>();
    }

    public override void Initialize ()
    {
        if (Note.TagTable.Lookup ("latex_code") == null) {
            NoteTag latex_code_tag = new NoteTag ("latex_code");
            /* Invisible causes instability, replaced by Size = 1
             * But it seems not to work in Windows. */
            //latex_code_tag.Invisible = true;
            latex_code_tag.Size = 1;
            latex_code_tag.Editable = false;
            latex_code_tag.CanSerialize = false;
            Note.TagTable.Add (latex_code_tag);
        }
        if (!Note.TagTable.IsDynamicTagRegistered ("latex_image")) {
            Note.TagTable.RegisterDynamicTag ("latex_image", typeof (LatexImageTag));
        }
    }

    public override void Shutdown ()
    {
        if (HasWindow) {
            Window.Editor.MoveCursor -= OnMoveCursor;
        }
    }

    public override void OnNoteOpened () 
    {
        images = new List<LatexImage>();
        CheckLaTeX ();

        Buffer.InsertText += OnInsertText;
        Buffer.DeleteRange += OnDeleteRange;
        Window.Editor.MoveCursor += OnMoveCursor;
    }

    public void ImageGenerated ()
    {
        CheckLaTeX();
    }

    private bool findMathCode(Gtk.TextIter pos, out Gtk.TextIter match_start, out Gtk.TextIter match_end)
    {
        Gtk.TextIter match_start_tmp, match_end_tmp, match_unused;

        match_start = Buffer.EndIter;
        match_end = Buffer.EndIter;
        bool found = false;
        
        if (pos.ForwardSearch("\\[", 0, out match_start_tmp, out match_unused, Buffer.EndIter) &&
                        match_unused.ForwardSearch("\\]", 0, out match_unused, out match_end_tmp, Buffer.EndIter)) {
            if (!found || match_start_tmp.Offset < match_start.Offset) {
                found = true;
                match_start = match_start_tmp;
                match_end = match_end_tmp;
            }
        }
        if (pos.ForwardSearch("\\(", 0, out match_start_tmp, out match_unused, Buffer.EndIter) &&
                        match_unused.ForwardSearch("\\)", 0, out match_unused, out match_end_tmp, Buffer.EndIter)) {
            if (!found || match_start_tmp.Offset < match_start.Offset) {
                found = true;
                match_start = match_start_tmp;
                match_end = match_end_tmp;
            }
        }
        if (IsDollarEnabled()) {
            if (pos.ForwardSearch("$", 0, out match_start_tmp, out match_unused, Buffer.EndIter) &&
                            match_unused.ForwardSearch("$", 0, out match_unused, out match_end_tmp, Buffer.EndIter)) {
                if (!found || match_start_tmp.Offset < match_start.Offset) {
                    found = true;
                    match_start = match_start_tmp;
                    match_end = match_end_tmp;
                }
            }
        }
        return found;
    }

    void CheckLaTeX () 
    {
        if (checkLatex_running) return;

        checkLatex_running = true;

        IDictionary<int, LatexImage> images_by_pos = null;
        bool images_by_pos_valid = false;

        try {
            Gtk.TextIter pos = Buffer.StartIter;
            pos.ForwardLine();
            while (true) {
                Gtk.TextIter match_start = pos;
                Gtk.TextIter match_end = pos;

                if (!findMathCode(pos, out match_start, out match_end))
                    break;

                Gtk.TextIter image_pos = match_start;
                image_pos.BackwardChar();

                if (!images_by_pos_valid) {
                    images_by_pos = new Dictionary<int, LatexImage> ();
                    foreach (LatexImage img in images) images_by_pos[img.ImagePosition.Offset] = img;
                    images_by_pos_valid = true;
                    /* TODO Can the Dictionary become invalid if the text
                     * is changed by the user while this method runs? */
                }

                Gtk.TextMark posMark = Buffer.CreateMark(null, match_end, true);
                LatexImage image = null;
                if (images_by_pos.ContainsKey(image_pos.Offset)) image = images_by_pos[image_pos.Offset];
                
                if (CheckLatexImage(image, image_pos, match_start, match_end))
                    images_by_pos_valid = false;

                pos = Buffer.GetIterAtMark(posMark);
            }
        } finally {
            checkLatex_running = false;
        }
    }

    public void OpenLatexImage (LatexImage image)
    {
        if (images.Contains(image))
        {
            TextIter pos = image.ImagePosition;
            pos.ForwardChars(3);
            Buffer.PlaceCursor(pos);
            CheckLaTeX();
        }
    }

    bool CheckLatexImage(LatexImage image, Gtk.TextIter image_pos, Gtk.TextIter code_start, Gtk.TextIter code_end)
    {
        Gtk.TextIter cursor = Buffer.GetIterAtMark(Buffer.InsertMark);
        bool cursorInCode = (image_pos.Compare(cursor) <= 0 && code_end.Compare(cursor) >= 0);

        if (image != null) {
            if (cursorInCode) {
                image.Open();
                images.Remove(image);
                return true;
            }
            return false;
        } else {
            if (cursorInCode) return false;

            string code = code_start.GetText(code_end);
            Gdk.Pixbuf pixbuf = manager.GetImage(code, this);
            if (pixbuf != null) {
                LatexImageTag image_tag = (LatexImageTag) Note.TagTable.CreateDynamicTag ("latex_image");
                image_tag.Addin = this;
                images.Add(new LatexImage(code_start, code_end, pixbuf, image_tag));
                return true;
            }
            return false;
        }
    }

    void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
    {
        CheckLaTeX ();
    }

    void OnInsertText (object sender, Gtk.InsertTextArgs args)
    {
        CheckLaTeX ();
    }

    void OnMoveCursor (object sender, Gtk.MoveCursorArgs args)
    {
        /* TODO this is not called when the cursor is placed with the mouse */
        CheckLaTeX ();
    }

    public static string GetHeader()
    {
        String header = (string) Preferences.Get(PREFERENCES_KEY_HEADER);
        if (header == "" || header == null)
        {
            header = DEFAULT_HEADER;
            Preferences.Set(PREFERENCES_KEY_HEADER, (string) header);
        }
        return header;
    }

    public static string GetFooter()
    {
        String footer = (string) Preferences.Get(PREFERENCES_KEY_FOOTER);
        if (footer == "" || footer == null)
        {
            footer = DEFAULT_FOOTER;
            Preferences.Set(PREFERENCES_KEY_FOOTER, (string) footer);
        }
        return footer;
    }

    public static bool IsDollarEnabled()
    {
        object enabled = Preferences.Get(PREFERENCES_KEY_DOLLAR_ENABLED);
        if (enabled == null)
        {
            enabled = DEFAULT_DOLLAR_ENABLED;
            Preferences.Set(PREFERENCES_KEY_DOLLAR_ENABLED, (bool) enabled);
        }
        return (bool) enabled;
    }

    public static void SetHeaderFooterAndDollarEnabled(string header, string footer, bool dollar_enabled)
    {
        Preferences.Set(PREFERENCES_KEY_HEADER, header);
        Preferences.Set(PREFERENCES_KEY_FOOTER, footer);
        Preferences.Set(PREFERENCES_KEY_DOLLAR_ENABLED, dollar_enabled);
        manager.ClearImageCache();
    }
}
}
