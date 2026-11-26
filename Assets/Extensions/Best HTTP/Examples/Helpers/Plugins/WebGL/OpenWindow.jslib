/*
 * Based on:
 *  https://github.com/valyard/UnityWebGLOpenLink
 */

var OpenWindowPlugin = {
    openWindow: function(link)
    {
    	var url = UTF8ToString(link) /* PIMLR: Unity 6 */;

		var func = function()
        {
        	window.open(url);
        	document.removeEventListener('mouseup', func);
        }

        document.addEventListener('mouseup', func);
    }
};

mergeInto(LibraryManager.library, OpenWindowPlugin);