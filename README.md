#Posh
misusing the browser as your windowing/drawing layer

requires:
* https://github.com/vvvv/NWamp
* https://github.com/vvvv/SVG branch: textEvents

to be cloned next to \Posh

comes with 
[autobahn](http://autobahn.ws/js/) and [jQuery](http://jquery.com)


##Code Overview

### WebServer.cs
* is an HTTP server
* serves the \web\posh.html as answer to any URL request only with a unique WebSocket port configured for each URL
* on /root it serves a listing of all saved *.xml documents

### WAMPServer.cs
* has a WebSocket listening on the specified port
* translates RPCs that come in via WebSocket to local calls
* registers a WAMPListener on the WebSocket
* registers Posh topics (pub/sub) on WAMP (Add, Update, Remove)
* publishes Posh RPCs (Dump, SetSessionName, KeyDown, KeyUp, KeyPress)
* has an __SvgEventCaller__
* handles a __RemoteContext__ per Session 
 * acts as kind of a buffer between changes to the local and the remote SVG DOM
 * keeps track of local DOM changes
 * provides them in Posh form (json/xml) on demand
* can publish Posh after every RPC (AutoPublish) or manually on Publish()
 
### SvgIdManager.cs
* is a custom ID manager for SVG
* gets __SvgEventCaller__ from WampServer
* OnAdd(element)
 * adds the element to the __RemoteContext__
 * calls __element.RegisterEvents()__ on the element handing it the __SvgEventCaller__
 * registers itself on element.AttributeChanged so it can reflect those changes on the __RemoteContext__
* OnDelete(element)
 * calls __element.UnregisterEvents()__

###/web
the web/js part of posh that 
* receives Posh snippets and 
 * manipulates the SVG DOM accordingly
 * registers RPCs as eventhandler 
* calls eventhandler