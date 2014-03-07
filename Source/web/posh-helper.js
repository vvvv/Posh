function setCookie(c_name, value, exdays)
{
	var exdate=new Date();
	exdate.setDate(exdate.getDate() + exdays);
	var c_value=escape(value) + ((exdays==null) ? "" : "; expires="+exdate.toUTCString());
	document.cookie=c_name + "=" + c_value;
}

function getCookie(c_name)
{
	var c_value = document.cookie;
	var c_start = c_value.indexOf(" " + c_name + "=");
	if (c_start == -1)
	  {
	  c_start = c_value.indexOf(c_name + "=");
	  }
	if (c_start == -1)
	  {
	  c_value = null;
	  }
	else
	  {
	  c_start = c_value.indexOf("=", c_start) + 1;
	  var c_end = c_value.indexOf(";", c_start);
	  if (c_end == -1)
	  {
	c_end = c_value.length;
	}
	c_value = unescape(c_value.substring(c_start,c_end));
	}
	return c_value;
}

$.fn.selectText = function()
{
	var doc = document;
	var element = this[0];
	console.log(this, element);
	if (doc.body.createTextRange) {
	   var range = document.body.createTextRange();
	   range.moveToElementText(element);
	   range.select();
	} else if (window.getSelection) {
	   var selection = window.getSelection();        
	   var range = document.createRange();
	   range.selectNodeContents(element);
	   selection.removeAllRanges();
	   selection.addRange(range);
	}
};
	
$.fn.textWidth = function()
{
	var html_org = $(this).html();
	var html_calc = '<span>' + html_org + '</span>';
	$(this).html(html_calc);
	var width = $(this).find('span:first').width();
	$(this).html(html_org);
	return width;
};

function mouseX(e)
{
	//log(e.pageX - $('#svg').offset().left);
	return e.pageX;// - $('#svg').offset().left;
}
	
function mouseY(e)
{//console.log($('#svg').offset().top);
	return e.pageY;// - $('#svg').offset().top;
}
	
function parseSVG(s) 
{
	var div = document.createElementNS('http://www.w3.org/1999/xhtml', 'div');
	div.innerHTML = s;

	var frag = document.createDocumentFragment();
	while (div.firstChild.firstChild)
		frag.appendChild(div.firstChild.firstChild);
		
	return frag;
}

function removeFirstLine(data)
{
	var lines = data.split('\n');
	// remove one line, starting at the first position
	lines.splice(0,1);
	// join the array back into a single string
	var data = lines.join('\n');
	return data;
}

var before;
function startTimer()
{
	before = Date.now();
}

function stopTimer()
{
	var diff = Date.now() - before;
	//if (diff > 0)
	//	log("time elapsed: " + diff + "ms");
}

function log(msg)
{
	//console.log(msg);
}