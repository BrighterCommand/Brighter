//function codeBlockHandler(id, data, value)
function codeBlockHandler()
{
    // handle groups of snippets to make sure at least one from the group is always shown
    HandleSnippetGroups();
    
    // handle any remaining snippets that aren't in groups
	var spanElements = document.getElementsByTagName("span");
	for(var i = 0; i < spanElements.length; ++i)
	{
	    var devlang = spanElements[i].getAttribute("codeLanguage");
	    if (devlang == null) continue;
	    
	    if (HasSnippetGroupAncestor(spanElements[i])) continue;
	    
        var checkboxId = GetDevlangCheckboxId(devlang);
	    if (checkboxId != null && checkboxId != "")
	    {
            if (docSettings[checkboxId] == "on")
		        spanElements[i].style.display = "";
            else
		        spanElements[i].style.display = "none";
	    }
	}
}

function HasSnippetGroupAncestor(object)
{
    var parent = object.parentElement;
    if (parent == null) return false;
    
    var className = parent.className;
    if (className != null && className == "snippetgroup")
        return true

    return HasSnippetGroupAncestor(parent);
}

function HandleSnippetGroups()
{
    var divs = document.getElementsByTagName("DIV");
    var divclass;
    for (var i = 0; i < divs.length; i++)
    {
        divclass = divs[i].className;
        if (divclass == null || divclass != "snippetgroup") continue;
        
        // if all snippets in this group would be hidden by filtering display them all anyhow
        var unfilteredCount = GetUnfilteredSnippetCount(divs[i]);
        
	    var spanElements = divs[i].getElementsByTagName("span");
	    for(var j = 0; j < spanElements.length; ++j)
	    {
	        var devlang = spanElements[j].getAttribute("codeLanguage");
	        if (devlang == null) continue;

            var checkboxId = GetDevlangCheckboxId(devlang);
	        
	        // for filtered devlangs, determine whether they should be shown/hidden
	        if (checkboxId != null && checkboxId != "")
	        {
	            if (unfilteredCount == 0 || docSettings[checkboxId] == "on")
		            spanElements[j].style.display = "";
                else
		            spanElements[j].style.display = "none";
	        }
	    }
    }
}

function GetUnfilteredSnippetCount(group)
{
    var count = 0;
    var spanElements = group.getElementsByTagName("span");
    for(var i = 0; i < spanElements.length; ++i)
    {
        var devlang = spanElements[i].getAttribute("codeLanguage");
        var checkboxId = GetDevlangCheckboxId(devlang);
        if (checkboxId != null && checkboxId != "")
        {
            if (docSettings[checkboxId] == "on")
	            count++;
        }
    }
    return count;
}

function GetDevlangCheckboxId(devlang)
{
    switch (devlang)
    {
        case "VisualBasic":
        case "VisualBasicDeclaration":
        case "VisualBasicUsage":
            return devlangsMenu.GetCheckboxId("VisualBasic");
        case "CSharp":
            return devlangsMenu.GetCheckboxId("CSharp");
        case "ManagedCPlusPlus":
            return devlangsMenu.GetCheckboxId("ManagedCPlusPlus");
        case "JScript":
            return devlangsMenu.GetCheckboxId("JScript");
        case "JSharp":
            return devlangsMenu.GetCheckboxId("JSharp");
        case "JavaScript":
            return devlangsMenu.GetCheckboxId("JavaScript");
        case "XAML":
            return devlangsMenu.GetCheckboxId("XAML");
        case "FSharp":
            return devlangsMenu.GetCheckboxId("FSharp");
        default:
            return "";
    }
}

// update stylesheet display settings for spans to show according to user's devlang preference
function styleSheetHandler(oneDevlang)
{
    var devlang = (oneDevlang != "") ? oneDevlang : GetDevlangPreference();

    var sd = getStyleDictionary();

    // Ignore if not found (Help Viewer 2)
    if(typeof(sd['span.cs']) == "undefined")
        return;

    if (devlang == 'cs') {
        sd['span.cs'].display = 'inline';
        sd['span.vb'].display = 'none';
        sd['span.cpp'].display = 'none';
        sd['span.nu'].display = 'none';
        sd['span.fs'].display = 'none';
    } else if (devlang == 'vb') {
        sd['span.cs'].display = 'none';
        sd['span.vb'].display = 'inline';
        sd['span.cpp'].display = 'none';
        sd['span.nu'].display = 'none';
        sd['span.fs'].display = 'none';
    } else if (devlang == 'cpp') {
        sd['span.cs'].display = 'none';
        sd['span.vb'].display = 'none';
        sd['span.cpp'].display = 'inline';
        sd['span.nu'].display = 'none';
        sd['span.fs'].display = 'none';
    } else if (devlang == 'nu') {
        sd['span.cs'].display = 'none';
        sd['span.vb'].display = 'none';
        sd['span.cpp'].display = 'none';
        sd['span.nu'].display = 'inline';
        sd['span.fs'].display = 'none';
    } else if (devlang == 'fs') {
        sd['span.cs'].display = 'none';
        sd['span.vb'].display = 'none';
        sd['span.cpp'].display = 'none';
        sd['span.nu'].display = 'none';
        sd['span.fs'].display = 'inline';
    }
}

function getStyleDictionary() {
    var styleDictionary = new Array();

    try
    {
        // iterate through stylesheets
        var sheets = document.styleSheets;

        for(var i=0; i<sheets.length;i++) {
            var sheet = sheets[i];

            // Ignore sheets at ms-help Urls
            if(sheet.href.substr(0,8) == 'ms-help:')
                continue;

            // get sheet rules
            var rules = sheet.cssRules;

            if(rules == null)
                rules = sheet.rules;

            // iterate through rules
            for(j=0; j<rules.length; j++) {
                var rule = rules[j];

                // Ignore ones that aren't defined
                if(rule.selectorText == null)
                    continue;

                // add rule to dictionary
                styleDictionary[rule.selectorText.toLowerCase()] = rule.style;
            }
        }
    }
    catch(e)
    {
        // Ignore errors (Help Viewer 2).  sheet.rules is inaccessible
        // due to security restrictions.
    }

    return(styleDictionary);
}

function GetDevlangPreference()
{
    var devlangCheckboxIds = devlangsMenu.GetCheckboxIds();
    var checkedCount = 0;
    var devlang;
    for (var key in devlangCheckboxIds)
    {
        if (docSettings[devlangCheckboxIds[key]] == "on")
        {
            checkedCount++;
            checkboxData = devlangsMenu.GetCheckboxData(devlangCheckboxIds[key]);
            var dataSplits = checkboxData.split(',');
            if (dataSplits.length > 1)
                devlang = dataSplits[1];
        }
    }
    return (checkedCount == 1 ? devlang : "nu");
}



function memberlistHandler()
{
   // get all the <tr> nodes in the document
	var allRows = document.getElementsByTagName("tr");
	var i;

	for(i = 0; i < allRows.length; ++i)
	{
	    var memberdata = allRows[i].getAttribute("data");
	    if (memberdata != null)
        {
	        if ((ShowBasedOnInheritance(memberdata) == false) || 
	            (ShowBasedOnVisibility(memberdata) == false) || 
	            (ShowBasedOnFramework(memberdata) == false) )
			        allRows[i].style.display = "none";
		    else
			    allRows[i].style.display = "";
        }
	}

	ShowHideFrameworkImages();
	ShowHideFrameworkSpans();
}

function ShowHideFrameworkImages()
{
    // show/hide img nodes for filtered framework icons
    // get all the <img> nodes in the document
	var allImgs = document.getElementsByTagName("img");

	for(var i = 0; i < allImgs.length; i++)
	{
	    var imgdata = allImgs[i].getAttribute("data");
	    if (imgdata != null)
        {
	        var checkboxId = imgdata + "Checkbox";            
            if (docSettings[checkboxId] != "on")
	        {
		        allImgs[i].style.display = "none";
	        }
		    else
			    allImgs[i].style.display = "";
        }
	}
}

function ShowHideFrameworkSpans()
{
    // show/hide img nodes for filtered framework icons
    // get all the <img> nodes in the document
	var allImgs = document.getElementsByTagName("span");

	for(var i = 0; i < allImgs.length; i++)
	{
	    var imgdata = allImgs[i].getAttribute("data");
	    if (imgdata != null)
        {
	        var checkboxId = imgdata + "Checkbox";            
            if (docSettings[checkboxId] != "on")
	        {
		        allImgs[i].style.display = "none";
	        }
		    else
			    allImgs[i].style.display = "";
        }
	}
}

function ShowBasedOnVisibility(memberdata)
{
    var isPublic = (memberdata.indexOf("public") != -1);
    var isProtected = (memberdata.indexOf("protected") != -1);
    var isPrivate = (memberdata.indexOf("private") != -1);
    var isExplicitII = (memberdata.indexOf("explicit") != -1);
    
    // if the public checkbox doesn't exist, default to showPublic == true
    var publicCheck = docSettings["PublicCheckbox"];
    var showPublic = (publicCheck == null) ? true : (publicCheck == "on");
    
    // if the protected checkbox doesn't exist, default to showProtected == true
    var protectedCheck = docSettings["ProtectedCheckbox"];
    var showProtected = (protectedCheck == null) ? true : (protectedCheck == "on");
  
    if ( (showProtected && isProtected) || (showPublic && isPublic) || isExplicitII || isPrivate)
        return true;

    return false;
}

function ShowBasedOnInheritance(memberdata)
{
    var isInherited = (memberdata.indexOf("inherited") != -1);
    var isDeclared = (memberdata.indexOf("declared") != -1);
    
    // if the inherited checkbox doesn't exist, default to showInherited == true
    var inheritedCheck = docSettings["InheritedCheckbox"];
    var showInherited = (inheritedCheck == null) ? true : (inheritedCheck == "on");
    
    // if the declared checkbox doesn't exist, default to showDeclared == true
    var declaredCheck = docSettings["DeclaredCheckbox"];
    var showDeclared = (declaredCheck == null) ? true : (declaredCheck == "on");
    
    if ( (showInherited && isInherited) || (showDeclared && isDeclared) )
        return true;

    return false;
}

function ShowBasedOnFramework(memberdata) {

    var splitData = memberdata.split(";");
    var foundNotNetfw = false;
    var frameworkFilter = document.getElementById('memberFrameworksMenu') != null;

    for (var i = 0; i < splitData.length; i++) {

        if (splitData[i] == "notNetfw") {
            foundNotNetfw = true;
            continue;
        }
        if (docSettings[splitData[i] + "Checkbox"] == "on")
            return true;
    }
    if (!foundNotNetfw && docSettings["netfwCheckbox"] == "on")
        return true;
    if (foundNotNetfw && docSettings["netfwCheckbox"] == null && !frameworkFilter)
        return true;

    return false;
}


function SetDropdownMenuLabel(menu, dropdown)
{	
    var dropdownLabelId = menu.GetDropdownLabelId();
	dropdown.SetActivatorLabel(dropdownLabelId);
	for (var i = 0; i < dropdowns.length; i++)
	{
	    dropdowns[i].reposition();
	}
}
