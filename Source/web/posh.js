/*
posh 0.1
brought to you by http://vvvv.org
*/

var ws;
var sessionID = "session";
var ups;

window.onload = function() 
{
	hideConfig();

	// connect to WAMP server
	ab.connect("ws://LOCALIP:WEBSOCKETPORT",
	// WAMP session was established
	function (session) 
	{
		ws = session;
		sessionID = session.sessionid();
		$('#sessionID').text('Session ID: ' + sessionID);

		ws.subscribe("add", onAdd);
		ws.subscribe("update-attribute", onUpdateAttribute);
		ws.subscribe("update-content", onUpdateContent);
		ws.subscribe("remove", onRemove);
		ws.call("dump").then(dumpResult); 

		//load configs now that session is established
		var val = getCookie('ups');
		if (!val)
			val = parseInt($('#ups').val());
		setUpdatesPerSecond(val);
		ups = 1000 / val;

		var name = getCookie('sessionName');
		if (name)
			sendSessionName(name);
		else
			name = sessionID; 
		setSessionName(name);
		
		$('#config').css('color', 'black');
	},

	// WAMP session is gone
	function (code, reason) 
	{
		// things to do once the session fails
		log(reason);
		ws = null;
		
		$('#config').css('color', 'red');
	});
}

var editorTargetCall, overTargetCall;
$(document).ready(function()
{
	$(document).on("contextmenu", function(e){
		  return false;
	}); 
			
	$(document).on('keydown', function(e) 
	{
		//prevent browser shortcuts
		if (e.ctrlKey)
			e.originalEvent.preventDefault();
		if ($('#editor').is(":visible"))
			return true;
		ws.call('keydown', e.ctrlKey, e.shiftKey, e.altKey, e.which);
	});
	
	$(document).on('keyup', function(e) 
	{
		if ($('#editor').is(":visible"))
			return true;
		ws.call('keyup', e.ctrlKey, e.shiftKey, e.altKey, e.which);
	});
	
	//vvvv hosted IE does not send keydown/up, only keypress?
	$(document).on('keypress', function(e) 
	{
		if ($('#editor').is(":visible"))
			return true;	
					
		ws.call('keypress', e.ctrlKey, e.shiftKey, e.altKey, String.fromCharCode(e.which));
		
		//don't scroll the page down on space
		if (e.keyCode == 32)
			e.originalEvent.preventDefault();
	});

	$('#svg').on('mousewheel', function(e)
	{
		if (overTargetCall)
		{
			ws.call(overTargetCall, e.originalEvent.wheelDelta, e.ctrlKey, e.shiftKey, e.altKey, sessionID);
			//prevent page from scrolling
			return false;
		}
		
		return true;
	});
	
	$('#svg').on('mousedown', function(e)
	{
		hideConfig();
		$('#editor').blur();
	});
	
	$('#editor').on('keyup', function(e) 
	{  
		if (editorTargetCall)
		{
			var $ed = $('#editor');
			if (e.keyCode == 13)
			{
				//execute
				$ed.blur();
			}
			else if (e.keyCode == 27)
			{
				//just hide the editor
				$ed.html($ed.data('before'));
				$ed.hide();
			}
			
			//get cursor position
			var c = $ed.caret();
			//adapt width of editor
			$ed.width($ed.textWidth() + 20);
			//set cursor back in place
			$ed.caret(c);
		}
	});
	
	$('#editor').blur(function(e)
	{
		//if value now different than before call(newvalue)
		var $ed = $('#editor');
		if ($ed.data('before') !== $ed.html())
		{
			ws.call(editorTargetCall, $ed.text().trim(), sessionID);
			editorTargetCall = null;
		}
		log('blurred');
		//hide editor
		$ed.hide();
		//re-show original text
		$ed.data('target').show();
	});
	
	$('#ups').bind('change', function(e) {
		e.preventDefault();
		var val = $(this).val();
		$('#upsdisp').text('Max updates/second: ' + val);
		setCookie('ups', val);
		
		ups = 1000 / val;
	});
	
	//ie hack
	if (navigator.userAgent.match('Trident') )
	{
		$('<link rel="stylesheet" type="text/css" href="ie.css" />').appendTo("head");
	}
});

function showConfig()
{
	$('#config').css('transform', 'translateY(0px)');
}

function hideConfig()
{
	var off = $('#config').height() - 12;
	$('#config').css('transform', 'translateY(' + off + 'px)');
}

function setSessionName(name)
{
	$('#sessionName').val(name);
}

function setUpdatesPerSecond(ups)
{
	$('#ups').val(ups);
	$('#upsdisp').text('Max updates/second: ' + ups);
}

function setLastChangeName(name)
{
	$('#lastChange').text('Last Change: ' + name);
}

function sessionNameKeyUp(e)
{
	if (e.keyCode == 13)
	{
		var name = $('#sessionName').val().trim();
		sendSessionName(name);
		setCookie('sessionName', name, 10);
	}		
}

function sendSessionName(name)
{
	ws.call('setSessionName', sessionID, name).then(setSessionName);
}

function dumpResult(data) 
{
	//log(data);
	//remove doctype
	data = removeFirstLine(data);

	//$('#svg').empty();
	var svg = document.getElementById('svg'); 
	while (svg.lastChild) 
	{
		svg.removeChild(svg.lastChild);
	}
	svg.appendChild(parseSVG(data));

	updateEvents(document);
	
	//reset the lookup after every dump
	//specifically necessary for startup
	elementLookup = {};
}

function updateEvents(doc)
{
	var now, last, diff;
	//replace events
	$('[onclick]', doc).each(function()
	{
		var call = $(this).attr('onclick');
		$(this).on('click', function(e) 
		{ 
			ws.call(call, mouseX(e), mouseY(e), e.which, 1, e.altKey, e.shiftKey, e.ctrlKey, sessionID);
		});
		
		$(this).on('dblclick', function(e) 
		{ 
			ws.call(call, mouseX(e), mouseY(e), e.which, 2, e.altKey, e.shiftKey, e.ctrlKey, sessionID);
			log('dbl');
		});
		$(this).removeAttr('onclick');
	});
	
	$('[onmousedown]', doc).each(function()
	{
		var call = $(this).attr('onmousedown');
		$(this).on('mousedown', function(e) 
		{ 
			e.originalEvent.preventDefault();
			mouseDownTarget = e.target;
			ws.call(call, mouseX(e), mouseY(e), e.which, 1, e.altKey, e.shiftKey, e.ctrlKey, sessionID);
			//log(call + ': ' + e.which + ': ' + mouseDownTarget);
		});
		$(this).removeAttr('onmousedown');
	});
	
	$('[onmouseup]', doc).each(function()
	{
		var call = $(this).attr('onmouseup');
		$(this).on('mouseup', function(e) 
		{ 
			ws.call(call, mouseX(e), mouseY(e), e.which, 1, e.altKey, e.shiftKey, e.ctrlKey, sessionID);
			mouseDownTarget = null;
			//log(call + ': ' + e.which);
		});
		$(this).removeAttr('onmouseup');
	});
	
	$('[onmousemove]', doc).each(function()
	{
		var call = $(this).attr('onmousemove');
		$(this).on('mousemove', function(e) 
		{ 
			now = e.timeStamp;
			diff = now - last;
			if (diff < ups)
				return;
			last = now;

			ws.call(call, mouseX(e), mouseY(e), e.which, 1, e.altKey, e.shiftKey, e.ctrlKey, sessionID);
		});
		$(this).removeAttr('onmousemove');
	});
	
	$('[onmouseover]', doc).each(function()
	{
		var call = $(this).attr('onmouseover');
		$(this).on('mouseover', function(e) 
		{ 
			var to = call.lastIndexOf("/");
			overTargetCall = call.substring(0, to) + "/onmousescroll";
			ws.call(call, mouseX(e), mouseY(e), e.which, 1, e.altKey, e.shiftKey, e.ctrlKey, sessionID);
		});
		$(this).removeAttr('onmouseover');
	});
	
	$('[onmouseout]', doc).each(function()
	{
		var call = $(this).attr('onmouseout');
		$(this).on('mouseout', function(e) 
		{ 
			overTargetCall = null;
			ws.call(call, mouseX(e), mouseY(e), e.which, 1, e.altKey, e.shiftKey, e.ctrlKey, sessionID);
		});
		$(this).removeAttr('onmouseout');
	});
	
	$('[onchange]', doc).each(function()
	{
		var call = $(this).attr('onchange');
		$(this).on('mousedown', function(e) 
		{ 
			if (e.which == 1)
				return true;
			var $this = $(this);
			var $ed = $('#editor');
			//if editor is currently visible just exit so that it first blurs
			if ($ed.is(":visible"))
				return true;
			
			//set call msg
			var to = call.lastIndexOf("/");
			editorTargetCall = call.substring(0, to) + "/onchange";

			//save current value
			$ed.data('before', $this.text());
			$ed.data('target', $(this));

			//show editor
			$ed.css({left: $(this).offset().left, top: $(this).offset().top});
			$ed.css('font-family', $(this).css('font-family'));
			$ed.css('font-size', $(this).css('font-size'));
			$ed.css('color', $(this).css('fill'));
			var w, h;
			if ($this.width() != 0)
			{
				//this works only in chrome
				w = $this.width() + 20;
				h = $this.height();
			}
			else
			{
				//ie, ff
				var rect = this.getBoundingClientRect();
				w = rect.width + 20;
				h = rect.height;
			}
			
			$ed.width(w);
			$ed.height(h);
			$ed.html($this.text());
			$ed.show(100); 
			$ed.focus();
			$ed.selectText();
			
			//now hide text under editor
			$(this).hide();
			
			//don't bubble the mousedown further:
			return false;
		});
		$(this).removeAttr('onchange');
	});
}

function onAdd(topicUri, event) 
{
	log("add: ");
	log(event);
	var doc = $.parseXML(event);
	var svgSnippet = $(doc);

	updateEvents(svgSnippet);

	startTimer();
	$('#add', $(svgSnippet)).children().each(function()
	{
		//id to insert
		var id = $(this).attr('id');
		var insertBeforeID = $(this).attr('insertBeforeID');

		//check if id already exists in document
		if (document.getElementById(id))
			console.log('ignoring existing element: ' + id);
		else
		{
			//get parent id
			var parentID = id.substring(0, id.lastIndexOf("_"));
			var parent;
			
			//if parentID = "" append to root
			if (parentID == '')
				parent = document.getElementById('svg');
			else 
				parent = document.getElementById(parentID);
			
			if (insertBeforeID)
			{
				$(this).removeAttr('insertBeforeID');
				var sibling = document.getElementById(insertBeforeID);
				parent.insertBefore(this, sibling);
			}
			else
			{
				parent.appendChild(this);
			}
		}		
	});
	
	var name = $('#add', $(svgSnippet)).attr('sessionName');
	setLastChangeName(name + ": add");
	stopTimer();
}

var elementLookup = {};
function onUpdateAttribute(topicUri, event) 
{
	log("update attributes");
	log(event);
	startTimer();

	var data = JSON.parse(event);
	var $element, i, update;

	i = data.Updates.length;
	while ( i-- ) 
	{
		update = data.Updates[i];
		
		//get (cached) element
		$element = elementLookup[update.id] || (elementLookup[update.id] = $(document.getElementById(update.id)));
		//setting all attributes at once does not preserve casing of attributes but svg needs that
		//$element.attr(update.attributes);

		//setting attributes one after the other
		//using DOMElement.setAttribute() instead of jquery which ignores case of attributes
		if($element[0] !== undefined)
		for (var name in update.attributes)
			$element[0].setAttribute(name, update.attributes[name]);
	}
	if(data.SessionName !== undefined)
		setLastChangeName(data.SessionName + ": update");
	stopTimer();
}

function onUpdateContent(topicUri, event) 
{
	//log(event);
	startTimer();

	var data = JSON.parse(event);
	var $element, i, update;

	i = data.Updates.length;
	while ( i-- ) 
	{
		update = data.Updates[i];
		
		//get (cached) element
		$element = elementLookup[update.id] || (elementLookup[update.id] = $(document.getElementById(update.id)));
		
		//setting elements text content
		$element.text(update.content);
	}
	
	if(data.SessionName !== undefined)
		setLastChangeName(data.SessionName + ": update");
		
	stopTimer();
}

function onRemove(topicUri, event) 
{
	log("remove: ");
	log(event);
	startTimer();
	var data = JSON.parse(event);
	
	$('#svg_Overlays_hack').attr('visibility', 'true');
	for (var i = 0; i < data.RemoveIDList.length; i++) 
	{
		var elementToRemove = document.getElementById(data.RemoveIDList[i]);
		delete elementLookup[data.RemoveIDList[i]];

		//log(data.RemoveIDList[i]);
		$(elementToRemove).remove();
	}
	$('#svg_Overlays_hack').attr('visibility', 'hidden');
	
	setLastChangeName(data.SessionName + ": remove");
	stopTimer();
}
	
	