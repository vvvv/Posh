#Posh
misusing the browser as your windowing/interaction/drawing layer for your c#/.net desktop applications by streaming [SVG] (http://en.wikipedia.org/wiki/Scalable_Vector_Graphics) graphics via the [WAMP] (http://wamp.ws/) v1 protocol to browsers. For some more details read the [announcement] (http://vvvv.org/blog/posh-an-svg-based-flat-ui-framework-targeting-browsers).

##Applications using Posh
* [Timeliner] (https://github.com/vvvv/Timeliner)

##Code Overview
comes with 
* [autobahn](http://autobahn.ws/js/) 
* [jQuery](http://jquery.com)
* [WampSharp] (https://github.com/vvvv/WampSharp)
* [SVG] (https://github.com/vvvv/SVG)

### WebServer.cs
* is an HTTP server
* serves the \web\posh.html as answer to any URL request only with a unique WebSocket port configured for each URL
* on /root it serves a listing of all saved *.xml documents

### PoshServer.cs
* has a WebSocket listening on the specified port
* translates RPCs that come in via WebSocket to local calls
* registers a WAMPListener on the WebSocket
* registers Posh topics (pub/sub) on WAMP (Add, Update, Remove)
* publishes Posh RPCs (Dump, SetSessionName, KeyDown, KeyUp, KeyPress)
* has an __SvgEventCaller__
* handles a __RemoteContext__ per Session 
* can publish Posh after every RPC (AutoPublish) or manually on Publish()

### RemoteContext.cs
* acts as kind of a buffer between changes to the local and the remote SVG DOM
* keeps track of local DOM changes
* provides them in Posh form (json/xml) on demand

### SvgIdManager.cs
* is a custom ID manager for SVG
* gets __SvgEventCaller__ from WampServer
* OnAdd(element)
 * adds the element to the __RemoteContext__
 * calls __element.RegisterEvents()__ on the element handing it the __SvgEventCaller__
 * registers itself on element.AttributeChanged so it can reflect those changes on the __RemoteContext__
* OnDelete(element)
 * calls __element.UnregisterEvents()__
 
### DynamicRPC.cs
* represents a dynamic RPC for being able to be registered at the WampHost

###/web
the web/js part of posh that 
* receives Posh snippets and 
 * manipulates the SVG DOM accordingly
 * registers RPCs as eventhandler 
* calls eventhandler
