Adjutant
========

Adjutant is a Command Prompt replacement for Windows, but with a lot of features that make it a sort of personal assistant program. Like the Command Prompt, you can use it to browse your files and run them (with the standard Windows commands such as cd and dir), as well as perform more advanced tasks:

- Launch applications found in your Start Menu programs
- Edit text files
- Manage your to-do list
- Check Gmail and Twitter
- Browse Reddit
- Get weather information

![Screenshot: Adjutant]()

Besides having multi-colored text output, Adjutant can display text as links to programs and webpages, as well as display pictures and animated GIFs, which make it very convenient to browse text/image content such as Reddit or your Twitter homeline.

However Adjutant is still in pre-release, which means that you will probably encounter bugs and minor issues. Some of the modules are still in early development and will contain only minimal functionality.


Installation
--------------

1. (You need to have [.NET framework](http://www.microsoft.com/en-us/download/details.aspx?id=30653) installed on your computer)
2. Download [the release](https://github.com/Winterstark/Adjutant/releases)
3. Extract
4. Run Adjutant.exe


Usage
-------

After running Adjutant it will appear in your top-left corner of the screen, slightly translucent. It will autohide after several seconds, so if that happens move your mouse cursor near the top-left screen edge and it will reappear; you can also show/hide it using the global hotkey (the tilde key by default) or via the tray icon. This behaviour as well as visual appearance, font, and module settings can all be changed in Options.

![Screenshot: the help system]()

Adjutant has a rudimentary tutorial and a relatively comprehensive help system. Type "help" to get a list of commands, and then "help [command]" to learn more about a specific one. This readme will only introduce you to Adjutant's capabilities - use the help command to find out more.

### Window appearance

Click and drag the console window to move its position. Click and drag the bottom or right edge to resize the window. Note that Adjutant has a minimum and a maximum value for its window height: the min. value is used for a blank console; when the console fills up it begins to increase in height until the max. value is reached. To set the min. value just resize the window; to set the max. value hold Control pressed while resizing. You can also fine-tune these settings in Options.

The window also has two values for opacity - one applies when the window is active (the user is typing something in it), the other when it's passive (not focused by the user).

If autohide is enabled, after a specific time the console will hide itself using the designated action: 

* Simply become invisible
* Fade out
* Scroll up/down/left/right

![Screenshot: scrolling up]()

![Screenshot: fading out]()

### Console text appearance

By default, text is printed to the console in blocks of 3 characters at once, with a small delay between the blocks, to create a nice output animation. A particularly large text printout can be flushed instantly by pressing Enter during the output animation. You can also disable the animation in Options and use the standard Command Prompt all-at-once output.

Besides the animation settings, you can also modify the following:

* Show/hide prompt (e.g. "c:\")
* Echo user's commands
* Console font, style, and size
* Text color - can be customized for a variety of output types (standard text, error messages, links, etc.)

![Screenshot: Twitter module options]()

### Copying text

Double-click Adjutant to enable console text selection. You can also use the keyboard shortcut (F2). After you select and copy a segment, return to normal output mode by double-clicking again or by pressing either F2 or Escape.

### To-do manager

The to-do manager consists of two commands ("todo" and "done") with which you add tasks to your list and then mark them as done. A to-do list represents the tasks you plan to do *today*; however, any tasks that you do not complete will be transferred to next day's to-do list.

In Options you can specify the folder in which to save your to-do lists. If you use Dropbox or a similar cloud-storage service you could specify that folder as a save location so that your to-do lists would be synced.

### Pad module

The Pad module enables you to use Adjutant as a simple notepad-like text editor.

Note: if you resize the window while in Pad mode, Adjutant will remember your preference and automatically resize the window to that specific size when you enter Pad mode.

Pad can also be used to write and run code source files, and supports syntax highlighting for the following languages:

* Assembly
* C#
* HTML
* JavaScript
* MS SQL
* Postgre SQL
* Python
* VB Script
* XML

### Gmail checker

A basic Gmail checker. Your login details are saved using AES encryption.

### Twitter module

A Twitter client for reading the tweets in your timeline. Type "help twitter /init" to learn how to authorize Adjutant to read your account's timeline.

### Reddit module

Asdf

### File launcher

Asdf

### Weather module

Asdf


Current limitations and future plans
--------------

Asdf


Credits
--------------
* Pad module uses the [ScintillaNET control](https://scintillanet.codeplex.com/)
* Adjutant icon by ~Softcode @ [deviantART](http://www.deviantart.com/art/Deep-Blue-Console-69538223)
* Download progress GIF uses graphics from [preloaders.net](http://preloaders.net/)