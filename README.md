Tomboy LaTeX Addin
==================

Requirements
------------
This addin requires:
* tomboy-0.7 or higher
* LaTeX including the ams package dvipng on in the PATH

For older versions of tomboy please use an older version of this
addin/plugin since the tomboy interface has changed.

Build and Install
-----------------
Simply run

    ./autogen.sh
    ./configure
    make
    make install (as root)

It should install the addin in the tomboy addins directory.
You can then install it via the tomboy interface (or copy the Latex.dll
from the addins directory (usually /usr/lib/tomboy/addins) to your
$HOME/.tomboy/addins folder)

Using the Addin
------------------
Simply type LaTeX math formulas enclosed in \[...\] in your notes and they will
automatically be converted to images. Clicking on the image or moving the cursor
near it will reveal the markup again.

Please send me your problems/comments/suggestions/bugs/etc (in AUTHORS file).
