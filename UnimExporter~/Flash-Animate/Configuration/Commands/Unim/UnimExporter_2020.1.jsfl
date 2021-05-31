// © 2020 ARTISANIMATION.COM ALL RIGHTS RESERVED

// Document references
var version = "2020.1";

var docName; 
var docPath;
var docDirPath;

// Log file variables
var unimScriptFolder = "Commands/Unim/";
var debugLogFilePath = fl.configURI + unimScriptFolder + "Log";
var textExtension = ".txt";
var logUniqueId = "_";
var saveLogWithUniqueId = false;

// Settings
var settingsDirectory = 'Unim/Unim_'+version+'_Settings';
var defaultSettingsFileName = "DefaultSettings.csv";
var defaultSettingsFileURI;	// converted to URI

// Export extensions
var spriteSheetExtension = "_sheet";
var flagsExtension = "_flags.csv";

// Temporary disposable file names
var tempRenderName = "__tempRender__.swf";
var tempSandboxName = "___sandbox___.fla";
var tempSandboxFilePath = fl.configURI + unimScriptFolder + tempSandboxName;

// Sandbox identifiers
var trackSymbolIdentifier = "_t_";
var trackSpriteSymbolIdentifier = "_ts_";
var spriteSymbolIdentifier = "_s_";
var spriteParentExtension = "__spriteParentExtension__";

// Other Stuff
var isDebugLog = true;
var debugLogWarningText = "";
var exportDir;
var sceneWidth = 0;
var sceneHeight = 0;
var cleanUpFileList = [];

// Export Variables
var jsonMain = {};
jsonMain.version;
jsonMain.sceneWidth;
jsonMain.sceneHeight;
jsonMain.sheetWidth;
jsonMain.sheetHeight;
jsonMain.maxSprites;
jsonMain.colorsExported;
jsonMain.colorOperationsCombined;

// Export Objects
var jsonClips;
var jsonTriggerEvents;
var jsonAnimationDataAndSprites;
var jsonTrackObjects;


// Setting options
var settingsDict = {};
settingsDict['isIncludeHiddenLayers'] = "false";
settingsDict['isExportTrackData'] = "true";
settingsDict['isExportEvents'] = "true";
settingsDict['isCompressAnimData'] = "false";
settingsDict['isExportAnimationData'] = "true";
settingsDict['SpriteScale'] = '1';
settingsDict['SpriteSymbolSubstring'] = "_s_";
settingsDict['TrackSymbolSubstring'] = "_t_";
settingsDict['CustomizationsSymbolSubstring'] = "__";
settingsDict['ClipsLayerSubstring'] = "CLIPS";
settingsDict['EventsLayerSubstring'] = "EVENTS";
settingsDict['BorderPadding'] = "2";
settingsDict['ShapePadding'] = "2";
settingsDict['ExportName'] = "Animation";
settingsDict['ExportDestination'] = "/UnimExport/";
settingsDict['isMinifyJson'] = "false";
settingsDict['TruncateDecimal'] = "4";
settingsDict['isTruncateDecimal'] = "true";
settingsDict['isIncludeRGBA'] = "true";
settingsDict['isPrettifyJson'] = "true";
settingsDict['isCompressColorOperations'] = "false";
settingsDict['isExportClips'] = "true";
settingsDict['isExportAsJsonObj'] = "false";
settingsDict['isExportSeperatedSprites'] = "false";
settingsDict['isExportCustomizationSprites'] = "false";


// Run Unim Exporter
UnimExporter();

// Main Unim Exporter function
function UnimExporter ()
{
	// Delete Any Existing Sandbox Files 
	if ( FLfile.exists (tempSandboxFilePath)){
		if ( !FLfile.remove(tempSandboxFilePath))
		{
			DebugLog("    Could not delete: " + "___sandbox___");
		}
	}

	// Exit program if there insn't an available document to process
	if (fl.getDocumentDOM() == undefined){
		alert("WARNING: There is no document open. Please open the document you wish to export and try again.")
		return;
	};
	
	// Clear any existing debug output
	fl.outputPanel.clear();

	//disable flash message that appears if script has been running for a long time.
	fl.showIdleMessage(false); 
	
	// Initializers
	fl.getDocumentDOM().currentTimeline = 0; // Set Animate CC focus to root scene
	
	// Store document name and path
	docName = fl.getDocumentDOM().name.replace('.fla', '');	// Store name of document minus .fla extension
	docPath = fl.getDocumentDOM().pathURI;
	if (docPath == undefined) // Exit program if current document has not yet been saved
	{ 
		alert('WARNING: This document must be saved somewhere first before using Unim Exporter.');
		return;
	}
	docDirPath = docPath.substring( 0, docPath.lastIndexOf("/") + 1 ); // Get document path by eliminating any string past the last index of "/" 

	// Create default settings. If not available then creates one.
	if (!CheckForDefaultSettingsFile()) {return;} 
	
	// Update script settings variables from file
	if (!PopulateLocalSettingsVariablesFromCSV(defaultSettingsFileURI)){return;} 

	// Display Unim GUI
	if (!showGui()){return;}	
	
	// Re-enable flash message that appears if script has been running for a long time.
	fl.showIdleMessage(true); 
}


function showGui(){
	// GUI loop
	var xmlPanelOutput;
	while(true){
		xmlPanelOutput = showXMLPanel(XmlBasicString());
		if (xmlPanelOutput.reload != ""){
			try{
				eval(xmlPanelOutput.reload);
			} catch (e)	{
				DebugLog("could not evaluate returned xml script: " + xmlPanelOutput.reload);
			}			
		}
		else{
			break;
		}
	} 
	
	// Update settings variables
	if (xmlPanelOutput.dismiss != "cancel"){
		for (i in settingsDict){
			settingsDict[i] = eval("xmlPanelOutput."+i) ;//xmlPanelOutput.eval(i);
		}
	}
	
	// Run any method that is returned from xml
	if (xmlPanelOutput.runScript != "" ){
		eval(xmlPanelOutput.runScript);
	}

	return true;
}

function showXMLPanel(xmlBasicString){
	
	var tempXML = fl.configURI + "/Commands/-temp-unim-dialog-xml.xml"
	//var tempUrl2 = fl.configURI + "/Commands/temp-dialog-xmlAdvanced.xml"

	FLfile.write(tempXML, xmlBasicString);
	//FLfile.write(tempUrl2, XmlAdvancedString() );

	var xmlPanelOutput = fl.getDocumentDOM().xmlPanel(tempXML);
	
	FLfile.remove(tempXML);
	//FLfile.remove(tempUrl2);
	
	return xmlPanelOutput;
}

function PopulateLocalSettingsVariablesFromCSV(filePath){
	var strCSV = FLfile.read(filePath);
	var tmpArray = CSVToArray(strCSV,",");
	if (tmpArray != undefined){
		for (var i=0 ; i<tmpArray.length ; i++){ // Loop through variables from settings file
			var matchFound = false;
			for (j in settingsDict){ // Loop through local script variables
				if (tmpArray[i][0] == j){ // if match found
					settingsDict[j] = tmpArray[i][1];
					matchFound = true;
					break;
				}
			}
			if (!matchFound){
				alert("WARNING: no match for variable '" + tmpArray[i] + "' was found.");
			}
		}
	}
	else{
		alert('Failed to convert save file to array');
		return false;
	}
	
	RepairSettingsDictVariables();
	
	return true;
}

function RepairSettingsDictVariables(){
	// ExportDestination
	settingsDict['ExportDestination'] = findAndReplace(settingsDict['ExportDestination'],'\\','/');
	if (settingsDict['ExportDestination'].charAt(0) == '/'){
		 settingsDict['ExportDestination'] = settingsDict['ExportDestination'].substr(1);
	}
	if (settingsDict['ExportDestination'].slice(-1) != "/"){
		settingsDict['ExportDestination'] = settingsDict['ExportDestination'] + '/';
	}
}

function findAndReplace(string, target, replacement) {
	var i = 0, length = string.length; 
	for (i; i < length; i++) {
		string = string.replace(target, replacement); 
	} 
	return string;
}

// Create a settings directory if one doesn't yet exist
function CheckForDefaultSettingsFile(){	
	var settingsURI = FLfile.platformPathToURI(fl.configDirectory + 'Commands/' + settingsDirectory);
	defaultSettingsFileURI = FLfile.platformPathToURI(fl.configDirectory + 'Commands/'+ settingsDirectory +'/' + defaultSettingsFileName);

	// Check for settings directory
	if(!fl.fileExists(settingsURI)){
		if(!FLfile.createFolder( settingsURI ) ){
			alert('Failed to create the "/' + settingsDirectory + '/" settings directory!');
			return false;
		}
	}
	
	// Check for default settings file
	if (!FLfile.exists(defaultSettingsFileURI)){
		if(!CreateSettingsFileCSV(defaultSettingsFileURI)){
			return false;
		}
	}
	
	return true;
}



function CheckIfSpriteSymbolExists()
{
	var isSpriteSymbolExists = false;
	
	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
	for (i = 0 ; i < libraryItem.length; i++){ // loop through each library item
		if (libraryItem[i].name.indexOf(settingsDict['SpriteSymbolSubstring']) != -1 ){ // check if symbol contain SpriteSymbolSubstring			
			isSpriteSymbolExists = true;
			break;
		}
	}	

	return isSpriteSymbolExists;
}

function InitLogFile(){
	FLfile.write(debugLogFilePath + logUniqueId + textExtension, "");
	if(saveLogWithUniqueId){
		logUniqueId = "_" + FLfile.getModificationDate(debugLogFilePath + logUniqueId + textExtension);
	}
	else{
		logUniqueId = "_";
	}
}

function Export()
{
	debugLogWarningText = "";
	InitLogFile();

	// Version
	jsonMain.version = version;
	
	// Store Scene Dimensions
	sceneWidth = fl.getDocumentDOM().width;
	sceneHeight = fl.getDocumentDOM().height;
	// For export
	jsonMain.sceneWidth = sceneWidth;
	jsonMain.sceneHeight = sceneHeight;



	// Check if at least one symbol contains the SpriteSymbol string
	if (CheckIfSpriteSymbolExists() == false){
		alert("Error: Could not find any symbols containing the  '" + settingsDict['SpriteSymbolSubstring'] + "'  sub-text. Please check over your symbol names again.")
		return;
	}

	// Create directory to store final export output
	DebugLog("-- Creating export directory");
	exportDir = CreateDirectory( docDirPath + settingsDict['ExportDestination'] ); 
	if (exportDir == false) { // stop export if failed to create directory path
		return;
	}

	// Create sandbox of current document to prevent damaging original file.
	DebugLog("-- Setting up sandbox environment");
	SetupSandboxEnvironment(); 

	try {
		// Store animation flags to be added to JSON export
		if (eval(settingsDict['isExportClips'])){
			DebugLog("-- Processing Clips");
			jsonClips = ProcessAnimClips();
			if (ProcessAnimClips == null){
				if (!confirm("Could not find '" + settingsDict['ClipsLayerSubstring'] + "' layer. Would you like to continue export?")){
					CloseSandboxEnvironment();
					return;
				}
			}
		}

		// Store Events to be added to JSON export
		if (eval(settingsDict['isExportEvents'])){
			DebugLog("-- Processing Events");
			jsonEvents = ProcessEvents();
			if (ProcessEvents == null){
				if (!confirm("Could not find '" + settingsDict['EventsLayerSubstring'] + "' layer. Would you like to continue export?")){
					CloseSandboxEnvironment();
					return;
				}
			}
		}
			
		// Exporting a swf and reimporting it is necessary to export Animation Data and Track Data
		if (eval(settingsDict['isExportAnimationData']) || eval(settingsDict['isExportTrackData'])){
			DebugLog("-- Setting up swf for export");
			
			SetupSwfForExports();
			try{
				// Exporting track data
				if (eval(settingsDict['isExportTrackData'])){
					DebugLog("-- Generating Track data");
					jsonTrackObjects = ProcessTrackData();		
				}

				CleanUpTrackSpriteSymbolNameIdentifiers();

				// Clean up any track symbols		
				OptimizeBitmapExportSettings();
				
				// Export animation data
				if (eval(settingsDict['isExportAnimationData'])){
					DebugLog("-- Generating Unim Data");
					var sS = GenerateSpriteSheet();
					jsonAnimationDataAndSprites = ProcessAnimationData( sS );
				}

				// Export Seperate Sprites
				if (eval(settingsDict['isExportSeperatedSprites'])){
					DebugLog("-- Exporting seperated sprites");
					GenerateSeperatedSprite();
				}


				DebugLog("-- Closing loaded swf");
				CloseCurrentDocument();
			}
			catch(err){
				DebugLog("###################");
				DebugLog("##### FAILED ######");
				DebugLog("###################");
				alert("ERROR: Unim failed to complete the export.\n\n" + err + "  |  Line#" + err.lineNumber);
				fl.trace(err + "  |  Line#" + err.lineNumber);
				CloseCurrentDocument();
				CloseSandboxEnvironment();
				return;
			}
		}

		DebugLog("-- Exporting Unim Data File");
		ExportUnimData();

	}
	catch(err){
		DebugLog("###################");
		DebugLog("##### FAILED ######");
		DebugLog("###################");
		alert("ERROR: Unim failed to complete the export.\n\n" + err + "  |  Line#" + err.lineNumber);
		fl.trace(err + "  |  Line#" + err.lineNumber);
		CloseSandboxEnvironment();
		return;
	}


	DebugLog("-- Closing sandbox environment");
	CloseSandboxEnvironment();
	
	if (confirm("Export complete!\nOpen export directory?")){
		DebugLog("-- Opening export directory");
		OpenExportDirectoryOnComplete();
	}

	DebugLog("-- Removing excess files");
	CleanUp(); // remove all unecssary files.

	if(debugLogWarningText != ""){
		alert(debugLogWarningText);
	}
}

function OptimizeBitmapExportSettings(){
	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
	for (i = libraryItem.length-1;  i >= 0 ; i--){ // loop through each library item
		if (libraryItem[i].itemType == "bitmap"){	
			libraryItem[i].allowSmoothing = true; 
			libraryItem[i].compressionType = "lossless";
		}
	}	
}

function GenerateSeperatedSprite()
{
	var URI = docDirPath + settingsDict['ExportDestination'] + "/SeperatedSprites/";

	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
	for (i = 0;  i < libraryItem.length ; i++){ // loop through each library item
		if (libraryItem[i].itemType == "bitmap")
		{
			var imageFileURL = URI + libraryItem[i].name + ".png";
			libraryItem[i].exportToFile(imageFileURL);
		}
	}
}

function ExportUnimData(){
	
	// export animation data
	var URI = docDirPath + settingsDict['ExportDestination'] + settingsDict['ExportName']+'.json';
	
	var obj = {};
	obj = CombineObjects( obj, jsonMain );
	obj = CombineObjects( obj, jsonClips );
	obj = CombineObjects( obj, jsonEvents );
	obj = CombineObjects( obj, jsonTriggerEvents );
	obj = CombineObjects( obj, jsonAnimationDataAndSprites );
	obj = CombineObjects( obj, jsonTrackObjects );
		
	var priorText = "";
	var postText = "";
	
	if(eval(settingsDict['isExportAsJsonObj'])){
		priorText = "var " + settingsDict['ExportName'] + "=";
		postText = "";
	}

	var objString = JSONstringify(obj);
	
	if (eval(settingsDict['isMinifyJson'])){
		DebugLog("    Miniifying Json");
		objString = JsonMinify(objString);
	}
	else if ( eval(settingsDict['isPrettifyJson']) ){
		//DebugLog("    Prettifying Json");
		objString = JsonStringPrettify(objString);
	}

	if (!FLfile.write(URI, priorText + objString + postText )) { 
		DebugLog("WARNING: Failed to export animation data succesfully."); 
	}
	
	if (settingsDict['isCompressAnimData']){
		CreateCompressedDataFile(URI)
	}

	return;
}

function JsonStringPrettify(jsonString)
{
	var newString = "";
	var curlyBracketCount = 0;
	var c;
	var specifiedCurly = 2;

	//RunAntiCrashAlert();

	for ( var i = 0 ; i < jsonString.length ; i++ )
	{
		c = jsonString.charAt(i);

		// Track the curly Braces
		if ( c == "{" ){
			curlyBracketCount++;
		}
		else if ( c == "}" ){
			curlyBracketCount--;
		}

		if( curlyBracketCount == 1 && c == ":")
		{
			var j = i;
			var tmp = "";
			while ( jsonString.charAt(j) != undefined && jsonString.charAt(j) != "\t" && jsonString.charAt(j) != "\n" )
			{
				tmp = jsonString.charAt(j) + tmp;
				j--;
			}
			if (tmp.indexOf('"trackObjects":') !== -1){
				specifiedCurly = 3;
			}
			else {
				specifiedCurly = 2;
			}
		}

		// Assign Characters
		if (curlyBracketCount >= specifiedCurly){
			if ( c != "\n" && c != "\t" ){
				if(c == ",")
				{
					newString += ", ";
				}
				else
				{
					newString += c;
				}	
			}
		}
		else{
			newString += c;
		}	
	}

	return newString;
}

function ProcessTrackData()
{
	var trackObjects = {};
	trackObjects["trackObjects"] = {};

	var itemNameToTrackData = {};
	var r2d = 180/Math.PI;

	// Loop through all frames
	var frames = fl.getDocumentDOM().getTimeline().layers[0].frames;
	for(var i = 0 ; i < frames.length ; i++)
	{
		fl.getDocumentDOM().getTimeline().setSelectedFrames(i, i+1); // fixes bug where element depth does not get processed		
		var elmnts = frames[i].elements;
		var spritesAlreadyTrackedThisFrame = [];
		for (var j = 0 ; j < elmnts.length ; j++)
		{
			if (elmnts[j].elementType == "instance")
			{
				// Process if symbol instance name contains the track identifier and that it's the first occurance of that symbol this frame.
				if ( ( elmnts[j].libraryItem.name.indexOf(trackSymbolIdentifier) !== -1 || elmnts[j].libraryItem.name.indexOf(trackSpriteSymbolIdentifier) !== -1 ) 
					&& spritesAlreadyTrackedThisFrame.indexOf(elmnts[j].libraryItem.name) < 0 )
				{
					var obj = {};
					obj.frame = i;

					spritesAlreadyTrackedThisFrame.push(elmnts[j].libraryItem.name); // prevents more then one track data for same frame
					var matrix = elmnts[j].matrix;

					obj.position = {};
					obj.position.x = matrix.tx;
					obj.position.y = fl.getDocumentDOM().height - matrix.ty;
					obj.position.z = elmnts[j].depth;

					var scale = getSymbolScaleVector(elmnts[j]);
					obj.scale = {};
					obj.scale.x = scale[0];
					obj.scale.y = scale[1];

					obj.rotation = (-Math.atan2( matrix.b , matrix.a )) * r2d ;

					// Trucation Operation
					if (eval(settingsDict['isTruncateDecimal'])){
						var TruncateDecimal = Number(settingsDict['TruncateDecimal']);
						obj.position.x = ReturnTruncatedValue(obj.position.x, TruncateDecimal);
						obj.position.y = ReturnTruncatedValue(obj.position.y, TruncateDecimal);
						obj.scale.x = ReturnTruncatedValue(obj.scale.x, TruncateDecimal);
						obj.scale.y = ReturnTruncatedValue(obj.scale.y, TruncateDecimal);
						obj.rotation = ReturnTruncatedValue(obj.rotation, TruncateDecimal);
					}


					
					var trackName = elmnts[j].libraryItem.name.replace(trackSpriteSymbolIdentifier , "").replace(trackSymbolIdentifier , "").replace(spriteSymbolIdentifier, "").replace(spriteParentExtension,"");

					if ( itemNameToTrackData[trackName] == undefined ){
						itemNameToTrackData[trackName] = [];
					}
					itemNameToTrackData[trackName].push(obj) ;
				}
			}
			else {
				DebugLog("The elementType for frame " + i + " element " + j + " is not recognized by this unim script");
			}
		}
	}
	
	trackObjects["trackObjects"] = [];
	// Create a file with the export data
	for (var key in itemNameToTrackData){
		var obj = {};
		obj.name = key;
		obj.transform = itemNameToTrackData[key];

		trackObjects["trackObjects"].push(obj);
	}

	return trackObjects;
}

function ReturnTruncatedValue(value, truncAmount){
	return Number(value.toFixed( truncAmount ) );
}

function getSymbolScaleVector(_element)
{ 
	var rotationRadians;
	if(isNaN(_element.rotation)) {
		rotationRadians = _element.skewX * Math.PI / 180;
	}
	else {
		rotationRadians = _element.rotation * Math.PI / 180;
	}   

	var sinRot = Math.sin(rotationRadians);
	var cosRot = Math.cos(rotationRadians);
	var SOME_EPSILON = 0.01;
	var flipScaleX, flipScaleY;

	if(Math.abs(cosRot) <  SOME_EPSILON) {
		// Avoid divide by zero. We can use sine and the other two elements of the matrix instead.
		flipScaleX = (_element.matrix.b / sinRot);
		flipScaleY = (_element.matrix.c / -sinRot);
	}
	else {
		flipScaleX = _element.matrix.a / cosRot;
		flipScaleY = _element.matrix.d / cosRot;
	}
	
	return [flipScaleX, flipScaleY];
}


function CleanUpTrackSpriteSymbolNameIdentifiers()
{
	// Excluding TrackSprite Symbol identifiers
	var libraryItem = fl.getDocumentDOM().library.items; // store Dictionary of child matrix for all bitmap embeded symbols.
	for (var i = libraryItem.length-1;  i >= 0 ; i--){
		if (libraryItem[i].name.indexOf(trackSpriteSymbolIdentifier) !== -1 ) {
			libraryItem[i].name = libraryItem[i].name.replace(trackSpriteSymbolIdentifier,'');
		}
	}
}


function CloseSandboxEnvironment(){
	var doc = fl.getDocumentDOM();
	fl.openDocument(docDirPath + docName + ".fla");	//opens original document
	fl.closeDocument(doc, false);
	//doc.close(false); 
	DebugLog("-- Loading original document");
	
}

function CreateDirectory( dirPath )
{

	if (!FLfile.exists(dirPath)){
		if (FLfile.createFolder(dirPath)) {
			DebugLog("    New folder created '" + dirPath + "'");
		}
		else{
			DebugLog("there was a problem creating the '" + dirPath + "'folder");
			return false;
		}
	}
	return dirPath;
}

function SetupSandboxEnvironment()
{
	// Create duplicate of current document and open it as a sandbox environment
	fl.saveDocument(fl.getDocumentDOM());	// save current document to be safe
	//var sandboxDocPath = docDirPath + tempSandboxName;
	fl.saveDocument(fl.getDocumentDOM() , tempSandboxFilePath); // Save current document as a Sandbox environment
	
	// Add the sandbox file to list of things to be deleted on completion.
	//cleanUpFileList.push(tempSandboxFilePath); 
}

function SetupSwfForExports()
{
	DeleteExcessBitmapsAndProcessRemainingOnes();
	ConvertFlaggedSpriteSymbolsToBitmap();
	var spriteNames = CreateAndStoreSpriteBitmapOrderOnFirstFrame();
	RenderAndLoadAnimationSWF(spriteNames);
}

function DeleteExcessBitmapsAndProcessRemainingOnes(){
	var listOfRemainingBitmaps = [];
	
	// Delete all bitmaps that don't contain Sprite identifier 
	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
	for (i = libraryItem.length-1;  i >= 0 ; i--){ // loop through each library item
		if (libraryItem[i].itemType == "bitmap"){	
			if (libraryItem[i].name.indexOf(settingsDict['SpriteSymbolSubstring']) === -1 ){ // check if symbol contain SpriteSymbolSubstring			
				fl.getDocumentDOM().library.deleteItem(libraryItem[i].name);	
			}
			else{
				listOfRemainingBitmaps.push(libraryItem[i].name);
			}
		}
	}	

	// Remove sprite symbol identifier from remaining bitmaps
	for (i = 0 ; i < listOfRemainingBitmaps.length ; i++ ){
		var newName = GenerateUniqueLibraryName( listOfRemainingBitmaps[i].replace(settingsDict['SpriteSymbolSubstring'],'') );
		fl.getDocumentDOM().library.selectItem( listOfRemainingBitmaps[i] );
		fl.getDocumentDOM().library.renameItem(newName + spriteSymbolIdentifier);
	}
}

function CloseCurrentDocument(){
	fl.closeDocument(fl.getDocumentDOM(), false);
}


function GenerateSpriteSheet()
{
	var sse = new SpriteSheetExporter;
	//sse.beginExport();
	
	sse.borderPadding = Math.round(settingsDict['BorderPadding']); // seems to break exports	
	sse.shapePadding = Math.round(settingsDict['ShapePadding']); // seems to break exports	
	
	sse.layoutFormat = "JSON";	
	sse.stackDuplicateFrames = false;	
	
	
	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
	fl.getDocumentDOM().library.selectNone();
	for (i = 0;  i < libraryItem.length ; i++){
		if (libraryItem[i].itemType == "bitmap" && libraryItem[i].name.indexOf(trackSymbolIdentifier) === -1 ){
			sse.addBitmap(libraryItem[i]);

			// if ( libraryItem[i].name.indexOf(trackSymbolIdentifier) != -1 ) {
			// 	fl.getDocumentDOM().library.deleteItem(libraryItem[i].name);
			// }

		}
	}
	
	sse.algorithm = "maxRects";	
	sse.allowRotate = true;	
	sse.allowTrimming = true;
	
	sse.autoSize = true; 
	
	var exportPath = docDirPath + settingsDict['ExportDestination'] + settingsDict['ExportName'] + spriteSheetExtension;
	var exportedSS = sse.exportSpriteSheet(exportPath ,{format:"png", bitDepth:32, backgroundColor:"#00000000"});
	
	if (spriteSheetExtension != ""){
		cleanUpFileList.push( exportPath + ".json" ); // add to list of files to be deleted.
	}

	return JSONparse(exportedSS);
}


function CreateAndStoreSpriteBitmapOrderOnFirstFrame(){ 
	var bitmapOrderList = [];
	
	fl.getDocumentDOM().currentTimeline = 0;
					
	var lyrs = fl.getDocumentDOM().getTimeline().layers;
		
	// make the first frame reserved for BitmapOrder
	for (var i = 0 ; i < lyrs.length ; i++){
		fl.getDocumentDOM().getTimeline().currentFrame = 0;
		fl.getDocumentDOM().getTimeline().setSelectedFrames([ i, 0, 1], true);
		fl.getDocumentDOM().getTimeline().insertFrames(1);
		fl.getDocumentDOM().getTimeline().cutFrames(0,1);
		fl.getDocumentDOM().getTimeline().pasteFrames(1, 2);
	}
	// Adding two extra frames for animation because for some reason not having the symbols animated breaks the hack.
	for (var i = 0 ; i < lyrs.length ; i++){
		fl.getDocumentDOM().getTimeline().currentFrame = 0;
		fl.getDocumentDOM().getTimeline().setSelectedFrames([ i, 0, 1], true);
		fl.getDocumentDOM().getTimeline().insertFrames(2);
	}
	
	// unlock first layer to allow for placement of bitmaps
	fl.getDocumentDOM().getTimeline().layers[0].locked = false;
	fl.getDocumentDOM().getTimeline().layers[0].visible = true;

	// Create animated Bitmaps symbol and populate it
	var bitmapAnimationList = "__BITMAPANIMATIONLIST__";
	fl.getDocumentDOM().library.addNewItem("graphic", bitmapAnimationList);
	fl.getDocumentDOM().getTimeline().currentFrame = 0;
	fl.getDocumentDOM().getTimeline().setSelectedFrames([ 0, 0, 1], true);	
	var lmnt = undefined;
	var MAXITER = 50;	
	for (var z = 0 ; z < MAXITER ; z++)
	{	
		fl.getDocumentDOM().library.addItemToDocument({x:0, y:0}, bitmapAnimationList);	
		lmnt = fl.getDocumentDOM().getTimeline().layers[0].frames[0].elements[0];

		if ( lmnt != undefined){ 
			break; 
		}
		else{ 
			if (z == MAXITER-1){
				throw "Could not add item to document...(flashCS6/animateCC bug) 822!";
			}
			DebugLog("    WARNING: Reattempting to add item to document...(flashCS6/animateCC bug)"); 
		}
	}
	fl.getDocumentDOM().library.editItem(bitmapAnimationList);
		var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
		for (i = 0; i < libraryItem.length ; i++){
			if (libraryItem[i].itemType == "bitmap"){
				bitmapOrderList.push(libraryItem[i].name);
				fl.getDocumentDOM().getTimeline().currentFrame = 0;
				fl.getDocumentDOM().getTimeline().setSelectedFrames([0, 0, 1], true);
				// check if element was added to prevent BUG.
				var lmnt = undefined;
				var MAXITER = 50;	
				for (var z = 0 ; z < MAXITER ; z++)
				{
					fl.getDocumentDOM().library.addItemToDocument({x:i*3, y:i*-3}, libraryItem[i].name);	//add tmp symbol to timeline with and offset (to prevent symbols switching bug) ( *3 for good measure )	
					fl.getDocumentDOM().getTimeline().currentFrame = 0;
					fl.getDocumentDOM().getTimeline().setSelectedFrames([ 0, 0, 1], true);
					lmnt = fl.getDocumentDOM().getTimeline().layers[0].frames[0].elements[0];
					if ( lmnt != undefined){ 
						break; 
					}
					else{ 
						if (z == MAXITER-1){
							throw "Could not add item to document...(flashCS6/animateCC bug) 822!";
						}
						DebugLog("    WARNING: Reattempting to add item to document...(flashCS6/animateCC bug)"); 
					}
				}
				
				fl.getDocumentDOM().getTimeline().addNewLayer();
				//fl.getDocumentDOM().library.addItemToDocument({x:0, y:0}, libraryItem[i].name); // <-- this should work but sometimes fails.
			}
		}

		// Animate the symbols to assure the z order is correct ( Assures z-Order hack works )
		var lyrs = fl.getDocumentDOM().getTimeline().layers;
		for (var i = 0 ; i < lyrs.length ; i++){
			fl.getDocumentDOM().getTimeline().currentFrame = 0;
			fl.getDocumentDOM().getTimeline().setSelectedFrames([ i, 0, 1], true);
			fl.getDocumentDOM().getTimeline().insertFrames(2);
		}
		for (var i = 0 ; i < lyrs.length ; i++){
			fl.getDocumentDOM().getTimeline().currentFrame = 2;
			fl.getDocumentDOM().getTimeline().setSelectedFrames([ i, 2, 3], true);
			fl.getDocumentDOM().getTimeline().createMotionTween();
			fl.getDocumentDOM().getTimeline().convertToKeyframes();
			var slctn = fl.getDocumentDOM().getTimeline().layers[i].frames[2].elements[0];
			fl.getDocumentDOM().getTimeline().currentFrame = 2;
			fl.getDocumentDOM().getTimeline().setSelectedFrames([ i, 2, 3], true);
			if (slctn != undefined){
				fl.getDocumentDOM().moveSelectionBy({x:(10 * i), y:(10 * i)});
			}
		}	
		if (fl.getDocumentDOM().getTimeline().layers.length-1 != bitmapOrderList.length){
			alert("Unim exporter ran into a problem. It only managed to track " + (fl.getDocumentDOM().getTimeline().layers.length-1) + " out of " + bitmapOrderList.length + " sprites. Modifying independent sprites may be broken. Press ok to continue the export");
		} 
	fl.getDocumentDOM().exitEditMode();

	return bitmapOrderList;
}


function RenderAndLoadAnimationSWF(_spriteNames){
	// render and load swf
	var swfPath = docDirPath + settingsDict['ExportDestination'] + tempRenderName;
	
	// render the scene and import it
	fl.getDocumentDOM().exportSWF(swfPath, true);
	
	//fl.getDocumentDOM().close(false); // TODO Try removing sanbox lwf here 
	
	fl.createDocument("timeline"); 
	fl.getDocumentDOM().importFile(swfPath);
	
	cleanUpFileList.push(swfPath); // add to list of files to be deleted.
	
	// clear first frame but don't delete it to make the animation data start on index 1 on a zero based system
	fl.getDocumentDOM().getTimeline().currentFrame = 0;
	fl.getDocumentDOM().getTimeline().setSelectedFrames([ 0, 0, 1], true);
	fl.getDocumentDOM().getTimeline().removeFrames(0, 2);
	fl.getDocumentDOM().getTimeline().currentFrame = 0;
	fl.getDocumentDOM().getTimeline().setSelectedFrames([ 0, 0, 1], true);
	if (fl.getDocumentDOM().selection != undefined ){
		try {
			fl.getDocumentDOM().deleteSelection(); // delete first frame which contains the bitmap order
		}
		catch(err){}
	}

	// rename bitmaps
	for (var i = 0 ; i < _spriteNames.length ; i++){
		if( !fl.getDocumentDOM().library.selectItem("Bitmap "+(i+1).toString(), true) ){
			DebugLog("There was a problem selecting: Bitmap "+(i+1).toString() + " renaming it to " + _spriteNames[i] );
		}
		else{
			var newName = _spriteNames[i].replace(spriteSymbolIdentifier,"");
			if (fl.getDocumentDOM().library.itemExists(newName)){

				// rename existing Item
				fl.getDocumentDOM().library.selectItem(newName)
				var rename = GenerateUniqueLibraryName( newName );
				fl.getDocumentDOM().library.renameItem( rename );
				
				// reselect original item
				fl.getDocumentDOM().library.selectItem("Bitmap "+(i+1).toString(), true)
			}
			fl.getDocumentDOM().library.renameItem(_spriteNames[i].replace(spriteSymbolIdentifier,"")); // sprite extension was only used to allow sprites to have non conflicting names. It is now safe to remove.
		}
	}


	//rename parent symbols
	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
	for (var i = 0;  i < libraryItem.length ; i++){
		if (libraryItem[i].itemType == "graphic"){
			fl.getDocumentDOM().library.editItem(libraryItem[i].name);
			fl.getDocumentDOM().getTimeline().currentFrame = 0;
				fl.getDocumentDOM().getTimeline().setSelectedFrames([0, 0, 1], true);
				var fillName = fl.getDocumentDOM().getCustomFill().bitmapPath;
				if (fillName != undefined){
					fl.getDocumentDOM().library.selectItem(libraryItem[i].name);
					if (fl.getDocumentDOM().library.itemExists(fillName + spriteParentExtension)){
						alert("WARNING: There seems to be a bug with \n\n" + fillName + "\n\nThis is Usually caused when two symbols have the exact same transformation matrix (pos, rotation, skew, and scale).\nAn easy solution is just to offest one of the symbols position slightly.");
						fl.trace("    WARNING: '"+fillName+"'' may have not exported correctly due to resembling matrix. Try to move its symbols position slightly.")
					}
					var newName = GenerateUniqueLibraryName(fillName + spriteParentExtension);
					fl.getDocumentDOM().library.renameItem(newName);
				}
			fl.getDocumentDOM().exitEditMode();
		}
	}

}

function ProcessAnimationData(_spriteSheetData)
{
	var animData = {};
	animData.sprites = [];
	animData.animationData = []; //array of frames in animation
	
	//Store child object matrix of each sprite onto a list.
	var ChildSpriteMatrix = {};
	var libraryItem = fl.getDocumentDOM().library.items; // store Dictionary of child matrix for all bitmap embeded symbols.
	for (var i = 0;  i < libraryItem.length ; i++){
		if (libraryItem[i].name.indexOf(spriteParentExtension) !== -1){
			fl.getDocumentDOM().library.editItem(libraryItem[i].name); // mod
			fl.getDocumentDOM().getTimeline().currentFrame = 0;
				fl.getDocumentDOM().getTimeline().setSelectedFrames([0, 0, 1], true);
				var childElement = fl.getDocumentDOM().getTimeline().layers[0].frames[0].elements[0];
				var childMatrix = childElement.matrix;
				childMatrix.a = childElement.scaleX;
				childMatrix.b = childElement.skewY;
				childMatrix.c = childElement.skewX;
				childMatrix.d = childElement.scaleY;
				childMatrix.tx = childElement.transformX;
				childMatrix.ty = childElement.transformY;
				ChildSpriteMatrix[libraryItem[i].name] = childMatrix;
			fl.getDocumentDOM().exitEditMode();
		}
	}
	fl.getDocumentDOM().currentTimeline = 0; // return back to root scene

	// texture variables
	var sheetWidth = _spriteSheetData['meta']['size']['w'];	
	var sheetHeight = _spriteSheetData['meta']['size']['h'];
	var frames = fl.getDocumentDOM().getTimeline().layers[0].frames;
	var maxElementLength = 0;
	
	// store max sprites used per frame by checking all frames
	for(var i = 0 ; i < frames.length ; i++){
		var elements = frames[i].elements;
		maxElementLength = Math.max(elements.length, maxElementLength);
	}
	
	// store imageParts data
	var SheetImageNameToId = {};
	var imageId = 0;
	for (i in _spriteSheetData['frames']){
		var newSheetImage = {};
		newSheetImage.id = imageId;	// images name
		newSheetImage.name = i;			// images id number
		newSheetImage.x = _spriteSheetData['frames'][i]['frame']['x']; 	// images top position
		newSheetImage.y = _spriteSheetData['frames'][i]['frame']['y']; 	// images bottom position
		newSheetImage.width = _spriteSheetData['frames'][i]['frame']['w']; 	// images pixel width
		newSheetImage.height = _spriteSheetData['frames'][i]['frame']['h']; 	// images pixel height
		newSheetImage.rotated = _spriteSheetData['frames'][i]['rotated'];		// is image rotated on sprite sheet to perserve image space
		animData.sprites.push(newSheetImage);
		
		SheetImageNameToId[i] = imageId; // stores dictionary of parts name to id for use when referencing sprites
		
		imageId++;
	}
	
	// loop through each frame of the animation
	for(var i = 0 ; i < frames.length ; i++){
		fl.getDocumentDOM().getTimeline().currentFrame = i;
		fl.getDocumentDOM().getTimeline().setSelectedFrames(i, i+1); // fixes bug where element depth does not get processed
		// Stores Frame Class
		frameData = []
		// loop through each element in the frame and overshoot up to maxframes		
		var elements = frames[i].elements;
		for (var j = 0 ; j < elements.length ; j++){

			if ( elements[j].libraryItem == undefined ){
				fl.trace( "Frame:" + i + " Element:" + j + " Doesn't seem to exist?? Look into what may be causing this.");
			}
			else{
				if ( elements[j].libraryItem.name.indexOf(trackSymbolIdentifier) !== -1 ){
					continue;
				}
			}

			var spriteData = {};	
			
			// set default sprite values
			spriteData.spriteID = -1;	// imageId
			// subtractive of combined operators
			if (eval(settingsDict['isIncludeRGBA'])){
				if( eval(settingsDict['isCompressColorOperations']) == false ){
					spriteData.color = {};
					spriteData.color.r = 0;	// color red
					spriteData.color.g = 0;	// color green
					spriteData.color.b = 0;	// color blue
					spriteData.color.a = 0;	// color alpha
				}
				spriteData.colorAdd = {};
				spriteData.colorAdd.r = 0;	// color red
				spriteData.colorAdd.g = 0;	// color green
				spriteData.colorAdd.b = 0;	// color blue
				spriteData.colorAdd.a = 0;	// color alpha
				
			}
			spriteData.scaleX = 0;	// scale X
			spriteData.scaleY = 0;	// scale Y
			spriteData.skewX = 0;	// skew X
			spriteData.skewY = 0;	// skew Y
			spriteData.positionX = 0;	// position X
			spriteData.positionY = 0;	// position Y
			spriteData.positionZ = 0;	// depth
			

			
			// TODO symbolCheck ( why are non symbols making it through ). don't need to include   elements[j] == '[object SymbolInstance]' 
			if (elements[j] == '[object SymbolInstance]' && (elements[j].libraryItem.name.indexOf(spriteParentExtension) !== -1 ||
			//elements[j].libraryItem.name.indexOf("_spriteB_") !== -1){
			elements[j].libraryItem.itemType == "bitmap") ){

				// get matrix of sprite whether it is layer in a symbol or not.
				var elementMatrix;
				if(elements[j].libraryItem.name.indexOf(spriteParentExtension) !== -1){
					var childMatrix = ChildSpriteMatrix[elements[j].libraryItem.name];
					elementMatrix = fl.Math.concatMatrix( childMatrix , elements[j].matrix );
				}
				
				//else if(elements[j].libraryItem.name.indexOf("_spriteB_") !== -1){
				else if(elements[j].libraryItem.itemType == "bitmap"){	
					elementMatrix = elements[j].matrix;
				}
				
				// init variable	
				var elementName = elements[j].libraryItem.name.replace(spriteParentExtension,'');
				spriteData.spriteID = SheetImageNameToId[elementName];
				
				
				var scaleX = (elementMatrix.a);
				var skewY = (elementMatrix.b);
				var skewX = (elementMatrix.c);
				var scaleY = (elementMatrix.d);
				var posY = (elementMatrix.ty);
				var posX = (elementMatrix.tx);
				
				var w = _spriteSheetData['frames'][elementName]['frame']['w'];
				var h = _spriteSheetData['frames'][elementName]['frame']['h'];
				var isRot = _spriteSheetData['frames'][elementName]['rotated'];
				// sprite scale matrix
				var B = {};
				B.m00 = isRot ? h : w;
				B.m01 = 0;
				B.m02 = 0;
				B.m03 = 0;
				
				B.m10 = 0;
				B.m11 = isRot ? w : h;
				B.m12 = 0;
				B.m13 = 0;
				
				B.m20 = 0;
				B.m21 = 0;
				B.m22 = 1;
				B.m23 = 0;
				
				B.m30 = 0;
				B.m31 = 0;
				B.m32 = 0;
				B.m33 = 1;
				
				// transform matrix
				var A = {};
				A.m00 = scaleX;
				A.m01 = -skewX;
				A.m02 = 0;
				A.m03 = posX;
				
				A.m10 = -skewY;
				A.m11 = scaleY;
				A.m12 = 0;
				A.m13 = -posY;
				
				A.m20 = 0;
				A.m21 = 0;
				A.m22 = 1;
				A.m23 = 0;
				
				A.m30 = 0;
				A.m31 = 0;
				A.m32 = 0;
				A.m33 = 1;
				
				// final matrix
				spriteData.scaleX = A.m00 * B.m00 + A.m01 * B.m10 + A.m02 * B.m20 + A.m03 * B.m30;
				spriteData.skewY = A.m00 * B.m01 + A.m01 * B.m11 + A.m02 * B.m21 + A.m03 * B.m31;
				spriteData.positionX = A.m00 * B.m03 + A.m01 * B.m13 + A.m02 * B.m23 + A.m03 * B.m33;

				spriteData.skewX = A.m10 * B.m00 + A.m11 * B.m10 + A.m12 * B.m20 + A.m13 * B.m30;
				spriteData.scaleY = A.m10 * B.m01 + A.m11 * B.m11 + A.m12 * B.m21 + A.m13 * B.m31;
				spriteData.positionY = A.m10 * B.m03 + A.m11 * B.m13 + A.m12 * B.m23 + A.m13 * B.m33;

				spriteData.positionZ = elements[j].depth;
				
				
				// color offset of sprite
				var alphaAdd = elements[j].colorAlphaAmount/255;
				var redAdd = elements[j].colorRedAmount/255;
				var greenAdd = elements[j].colorGreenAmount/255;
				var blueAdd = elements[j].colorBlueAmount/255;
				
				var alphaSubtract = elements[j].colorAlphaPercent / 100;
				var redSubtract = elements[j].colorRedPercent / 100;
				var greenSubtract = elements[j].colorGreenPercent / 100;
				var blueSubtract = elements[j].colorBluePercent / 100;
				
				
				if (eval(settingsDict['isIncludeRGBA'])){
					if (eval(settingsDict['isCompressColorOperations'])){
						// add + (1-Abs(add) )*(sub-1)  // additive takes priority in this formula ( Best solution I can think of. Works well)
						spriteData.colorAdd.r = redAdd + (1-Math.abs(redAdd)) * (redSubtract - 1);
						spriteData.colorAdd.g = greenAdd + (1-Math.abs(greenAdd)) * (greenSubtract - 1);
						spriteData.colorAdd.b = blueAdd + (1-Math.abs(blueAdd)) * (blueSubtract - 1);			
						spriteData.colorAdd.a = (alphaSubtract - 1) + alphaAdd;
					}
					else{
						spriteData.color.r = redSubtract;
						spriteData.color.g = greenSubtract;
						spriteData.color.b = blueSubtract;
						spriteData.color.a = alphaSubtract;

						spriteData.colorAdd.r = redAdd;
						spriteData.colorAdd.g = greenAdd;
						spriteData.colorAdd.b = blueAdd;
						spriteData.colorAdd.a = alphaAdd;
					}
				}
				
				
				if (eval(settingsDict['isTruncateDecimal'])){
					var TruncateDecimal = Number(settingsDict['TruncateDecimal']);
					if (eval(settingsDict['isIncludeRGBA'])){
						if ( !eval(settingsDict['isCompressColorOperations']) ){
							spriteData.color.r  = ReturnTruncatedValue(spriteData.color.r, TruncateDecimal);
							spriteData.color.g  = ReturnTruncatedValue(spriteData.color.g, TruncateDecimal);
							spriteData.color.b  = ReturnTruncatedValue(spriteData.color.b, TruncateDecimal);
							spriteData.color.a  = ReturnTruncatedValue(spriteData.color.a, TruncateDecimal);
						}

						spriteData.colorAdd.r  = ReturnTruncatedValue(spriteData.colorAdd.r, TruncateDecimal);
						spriteData.colorAdd.g  = ReturnTruncatedValue(spriteData.colorAdd.g, TruncateDecimal);
						spriteData.colorAdd.b  = ReturnTruncatedValue(spriteData.colorAdd.b, TruncateDecimal);
						spriteData.colorAdd.a  = ReturnTruncatedValue(spriteData.colorAdd.a, TruncateDecimal);
					}
					spriteData.scaleX = ReturnTruncatedValue(spriteData.scaleX, TruncateDecimal);
					spriteData.scaleY = ReturnTruncatedValue(spriteData.scaleY, TruncateDecimal);
					spriteData.skewX = ReturnTruncatedValue(spriteData.skewX, TruncateDecimal);
					spriteData.skewY = ReturnTruncatedValue(spriteData.skewY, TruncateDecimal);
					spriteData.positionX = ReturnTruncatedValue(spriteData.positionX, TruncateDecimal);
					spriteData.positionY = ReturnTruncatedValue(spriteData.positionY, TruncateDecimal);
					spriteData.positionZ = ReturnTruncatedValue(spriteData.positionZ, TruncateDecimal);
				}
				
				//var framesOuput = [ spriteData.spriteID, spriteData.red, spriteData.green, spriteData.blue, spriteData.alpha, spriteData.scaleX, spriteData.scaleY, spriteData.skewX, spriteData.skewY, spriteData.positionX, spriteData.positionY, spriteData.positionZ ];
				frameData.push( spriteData );	
				//frameData.s.push(spriteData);	
			}
			
			// populate framesData with one array of spriteTexture on each iteration
		}
		// populate animData with one array of frames on each iteration
		animData.animationData.push(frameData)
	}
	
	// general anim data
	jsonMain.version = version;
	jsonMain.sceneWidth = sceneWidth;
	jsonMain.sceneHeight = sceneHeight;
	jsonMain.sheetWidth = sheetWidth;		// texture sheet width
	jsonMain.sheetHeight = sheetHeight;	// texture sheet height
	jsonMain.maxSprites = maxElementLength; // max sprites used in any given frame on animation

	if (eval(settingsDict['isIncludeRGBA']))
	{
		jsonMain.colorsExported = true;
	}
	else
	{
		jsonMain.colorsExported = false;
	}

	if( eval(settingsDict['isCompressColorOperations'] ) == true )
	{
		jsonMain.colorOperationsCombined = true;
	}
	else
	{
		jsonMain.colorOperationsCombined = false;
	}

	return animData;


}


function CreateCompressedDataFile( uri )
{
	DebugLog("-- Compressing JSON");
	var compressorPath = FLfile.uriToPlatformPath(fl.configURI) + "Commands\\zzJsonCompressor\\jsonCompressor.exe";
	var animDataPath = FLfile.uriToPlatformPath(uri);
	var compressCommand = '"' + compressorPath + '" "' + animDataPath+'"' ;
	//DebugLog(compressCommand);
	
	FLfile.runCommandLine('"'+compressCommand+'"');

}


function ProcessAnimClips()
{
	var clips = {};
	clips["clips"] = [];

	var clipsLayerExists = false;
	var clipsLayerNum = undefined;

	// Check if CLIPS layer exists
	var maxLyr = fl.getDocumentDOM().getTimeline().layerCount;
	for (var i = 0; i < maxLyr; i++){
		if (fl.getDocumentDOM().getTimeline().layers[i].name == settingsDict['ClipsLayerSubstring']){
			clipsLayerExists = true;
			
			clipsLayerNum = i;
			var currFrame = 0;
			while (fl.getDocumentDOM().getTimeline().layers[clipsLayerNum].frames[currFrame] != undefined){
				var frameDuration = fl.getDocumentDOM().getTimeline().layers[clipsLayerNum].frames[currFrame].duration;
				
				clipName = fl.getDocumentDOM().getTimeline().layers[clipsLayerNum].frames[currFrame].name
				
				if (clipName != ""){
					obj = {}
					obj.name = clipName;
					obj.startFrame = currFrame+1;
					obj.duration = frameDuration;
					clips["clips"].push(obj);
				}
				currFrame += frameDuration;
			}
		}
	}

	if (clipsLayerExists == false){
		DebugLog("    No Animation Clips Layer exist.");
		return null;
	}
	
	else{
		//DebugLog("    Clips Generated successfully.");
		return clips;
	}
}

function ProcessEvents()
{
	var events = {};
	events["triggers"] = [];

	var eventsLayerExists = false;
	var eventsLayerNum = undefined;

	// Check if events layer exists
	var maxLyr = fl.getDocumentDOM().getTimeline().layerCount;
	for (var i = 0; i < maxLyr; i++){
		if (fl.getDocumentDOM().getTimeline().layers[i].name == settingsDict['EventsLayerSubstring']){
			eventsLayerExists = true;
			
			eventsLayerNum = i;
			var currFrame = 0;
			while (fl.getDocumentDOM().getTimeline().layers[eventsLayerNum].frames[currFrame] != undefined){
				var frameDuration = fl.getDocumentDOM().getTimeline().layers[eventsLayerNum].frames[currFrame].duration;
				
				eventName = fl.getDocumentDOM().getTimeline().layers[eventsLayerNum].frames[currFrame].name
				
				if (eventName != ""){
					obj = {}
					obj.frame = currFrame+1;
					obj.name = eventName;
					//obj.duration = frameDuration;
					events["triggers"].push(obj);
				}
				currFrame += frameDuration;
			}
		}
	}

	if (eventsLayerExists == false){
		DebugLog("    No Animation Events Layer exist.");
		return null;
	}
	
	else{
		//DebugLog("    Events Generated successfully.");
		return events;
	}
}

// Combines 2 javascript objects into one
function CombineObjects(obj, src) {
    for (var key in src) {
        if (src.hasOwnProperty(key)) obj[key] = src[key];
    }
    return obj;
}

function JsonMinify(jsonString){
	newString = "";
	var inString = false;
	for (var i=0 ; i < jsonString.length ; i++){
		if (jsonString[i] == '"'){
			inString = !inString;
		}
		if (!inString){
			if (jsonString[i] != ' ' && jsonString[i] != '\n' && jsonString[i] != '\r' && jsonString[i] != '\t'){
				newString += jsonString[i];
			}
		}
		else{
			newString += jsonString[i];
		}
		
	}
	return newString;
}

function clamp(num, min, max) {
  return num <= min ? min : num >= max ? max : num;
}

function CleanUp(){
	
	DebugLog("-- Deleting uneeded files")
	for (i = 0 ; i < cleanUpFileList.length ; i++){
		if ( FLfile.exists (cleanUpFileList[i])){
			if ( FLfile.remove(cleanUpFileList[i])){
				//DebugLog("    " + cleanUpFileList[i])
			}
			else{
				DebugLog("    Could not delete: " + cleanUpFileList[i])
			}
		}
	}
}

function OpenExportDirectoryOnComplete(){
	var directoryToOpen = FLfile.uriToPlatformPath(exportDir);
	if ( FLfile.exists (exportDir)){
		FLfile.runCommandLine('explorer "' + directoryToOpen.replace(/\//g,"\\"));
	}
	else{
		DebugLog("could not open the following directory: " + directoryToOpen);
	}
}

// Temporarily unlock and make layer visible
function tempUnlockAndShowLayer(){
	this.lyr;
	this.reLock;
	this.reVisible;

	// Temporarily unlock and make layer visible
	this.init = function(lyr){
		this.lyr = lyr;
		this.reLock = false;
		this.reVisible = false;
		if ( fl.getDocumentDOM().getTimeline().layers[this.lyr].locked){
			fl.getDocumentDOM().getTimeline().layers[this.lyr].locked = false;
			this.reLock = true;
		}
		if ( fl.getDocumentDOM().getTimeline().layers[this.lyr].visible != true){
			fl.getDocumentDOM().getTimeline().layers[this.lyr].visible = true;
			this.reVisible = true;
		}
	}
	
	// Re-lock and re-hidden
	this.revert = function(){
		if (this.reLock){
			fl.getDocumentDOM().getTimeline().layers[this.lyr].locked = true;
		}
		if (this.reVisible){
			fl.getDocumentDOM().getTimeline().layers[this.lyr].visible = false;
		}
	}

}

function GenerateUniqueLibraryName(_name){
	var i = 1;
	var isConfirmedUnique = false;
	var newName = _name;
	

	// itemExists is not case sensitive. So added a second check
	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items
	while (!isConfirmedUnique){
		while ( fl.getDocumentDOM().library.itemExists(newName) ){
			i++;
			newName = _name + " " + i.toString();
			
		}
		
		// double check if library item exists because item existst doesn't take case sensitivity into account. (at least in CS6)
		isConfirmedUnique = true;
		for (j = 0;  j < libraryItem.length ; j++){ // loop through each library item
			if (libraryItem[j].name.toLowerCase() ==  newName.toLowerCase()){
				isConfirmedUnique = false;
				newName = _name + " " + i.toString();
			}
		}
	}

	
	return newName;
}

function ConvertFlaggedSpriteSymbolsToBitmap(){
	var libraryItem = fl.getDocumentDOM().library.items; // store list of library items

	// process any symbols that need to be converted into a bitmap
	for (i = 0;  i < libraryItem.length ; i++){ // loop through each library item
		if ( // Process if symbol contain track or sprite substring
		(libraryItem[i].itemType == "graphic" || 
		libraryItem[i].itemType == "movie clip") && 
		(libraryItem[i].name.indexOf(settingsDict['SpriteSymbolSubstring']) !== -1 ||
		libraryItem[i].name.indexOf(settingsDict['TrackSymbolSubstring']) !== -1) 
		){ 			
			// Add focus and edit the selected library item
			fl.getDocumentDOM().library.editItem(libraryItem[i].name); // modify selected symbol

			//	UNLOCK AND UNHIDE ALL DESIRED LAYERS. KEYFRAME ALL FRAMES
			var lyrs = fl.getDocumentDOM().getTimeline().layers;
			var maxFrmLngth = fl.getDocumentDOM().getTimeline().frameCount;
			for (var j = 0; j < lyrs.length; j++) {

				// If layer is not visible and IncludeHiddenLayer is turned off lock it to prevent any unwanted behaviour
				if (!fl.getDocumentDOM().getTimeline().layers[j].visible && !eval(settingsDict['isIncludeHiddenLayers'] )){
					fl.getDocumentDOM().getTimeline().layers[j].locked = true;
				}
				else{
					fl.getDocumentDOM().getTimeline().layers[j].locked = false;
					fl.getDocumentDOM().getTimeline().layers[j].visible = true;
				}
				
				// If the current layer is not selected, make it selected
				if(fl.getDocumentDOM().getTimeline().currentLayer != j){
					fl.getDocumentDOM().getTimeline().currentLayer = j;
				}
			
				// keyframe every frame on current layer
				var frms = fl.getDocumentDOM().getTimeline().layers[j].frames;
				for (var k = 0; k < frms.length; k++) {
					// If frame is longer then 1 frame, key all it's frames.
					var frmDrtn = fl.getDocumentDOM().getTimeline().layers[j].frames[k].duration;
					if (frmDrtn > 1){
						fl.getDocumentDOM().getTimeline().convertToKeyframes(k,k+frmDrtn);
						if(fl.getDocumentDOM().getTimeline().layers[j].frames[k].tweenType != "none"){
							fl.getDocumentDOM().getTimeline().setFrameProperty('tweenType', 'none', k, k+frmDrtn);
						}
						
						k=k+frmDrtn-1; // no need to itterate through the recently . -1 because the script adds one each itteration.
					}
					else {
						if(fl.getDocumentDOM().getTimeline().layers[j].frames[k].tweenType != "none"){
							// If the layer the current frame is on isn't selected, make it selected
							if(fl.getDocumentDOM().getTimeline().currentLayer != j){
								fl.getDocumentDOM().getTimeline().currentLayer = j;
							}
							fl.getDocumentDOM().getTimeline().setFrameProperty('tweenType', 'none', k, k+1);
						}
					}
				}
			}
			
			// CONVERT FRAMES TO BITMAPS			
			fl.getDocumentDOM().getTimeline().currentLayer = 0; // go back to first layer

			// Transform all layers on a frame into a single bitmap
			var tmpExt = 1;
			for (var k = 0; k < maxFrmLngth; k++) {
				fl.getDocumentDOM().getTimeline().currentFrame = k;	// sets current frame to visible. Bug Fix
				fl.getDocumentDOM().selectAll();	// select all key frames from all layers on specified frame number
				
				// Skip over if black frame
				if (fl.getDocumentDOM().selection[0] == undefined){
					DebugLogWarning("Nothing to convert to bitmap for " + libraryItem[i].name + " at frame " + (k + 1) );
					continue;	
				}

				// Convert to bitmap 
				fl.getDocumentDOM().convertSelectionToBitmap();
				var newBitmapItemName = fl.getDocumentDOM().selection[0].libraryItem.name;

				// Select and rename newBitmap
				if(fl.getDocumentDOM().library.selectItem(newBitmapItemName)){
					// Create and process a "newName" for the bitmap to inherit
					var newName = libraryItem[i].name;
					if (maxFrmLngth > 1){
						newName =  newName + pad(tmpExt,2);
					}

					// Append track sprite extension.if both sprite and track identifiers are present
					if (libraryItem[i].name.indexOf(settingsDict['SpriteSymbolSubstring']) !== -1 && libraryItem[i].name.indexOf(settingsDict['TrackSymbolSubstring']) !== -1){
						newName = newName.replace(settingsDict['SpriteSymbolSubstring'],'');
						if (newName.indexOf(settingsDict['TrackSymbolSubstring']) === -1){
							DebugLog("    WARNING: Some characters from the Track and Sprite symbol identifiers where overlapping. Please make sure to seperate each identifier");
							DebugLogWarning("WARNING: Some characters from the Track and Sprite symbol identifiers where overlapping. Please make sure to seperate each identifier");
						}
						else{
							newName = newName.replace(settingsDict['TrackSymbolSubstring'],'');
						}
						newName = newName + trackSpriteSymbolIdentifier; // adding sprite extension to avoid name conflicts with other library items
					}

					// Append sprite extension. To increase chance of using desired sprite name on export.
					else if(libraryItem[i].name.indexOf(settingsDict['SpriteSymbolSubstring']) !== -1){
						newName = newName.replace(settingsDict['SpriteSymbolSubstring'],'');
						newName = newName + spriteSymbolIdentifier; // adding sprite extension to avoid name conflicts with other library items
					}
					
					// Append track extension. To increase chance of using desired sprite name on export.
					else if(libraryItem[i].name.indexOf(settingsDict['TrackSymbolSubstring']) !== -1){
						newName = newName.replace(settingsDict['TrackSymbolSubstring'],'');
						newName = newName + trackSymbolIdentifier;
					}
					
					// Apply a new name onto the bitmap
					fl.getDocumentDOM().library.renameItem(GenerateUniqueLibraryName(newName));
					tmpExt++;
				}
				else {
					DebugLog("Failed to select library item after converting to bitmap!");
				}
			}
			
			fl.getDocumentDOM().exitEditMode();
		}
	}
}

function CreateSettingsFileCSV(filePathURI){ // Creates settings file
	// create comma delimited string of all settings parameters
	var tmpStr = '';
	for (i in settingsDict){
		tmpStr+= i + ',' + settingsDict[i] + '\n'; 
	}
	tmpStr = tmpStr.replace(/\n$/, ""); //removes last new line.

	// write settings file
	if (!FLfile.write(filePathURI, tmpStr)){
		alert ("WARNING: God damnit!@#$ I failed at making your file \n\n" + filePathURI + "\n\n Sorry " );
		return false;
	}
	
	return true;
}

// convert CSV to array
function CSVToArray( strData, strDelimiter ){
	// Check to see if the delimiter is defined. If not, then default to comma.
	strDelimiter = (strDelimiter || ",");
	// Create a regular expression to parse the CSV values.
	var objPattern = new RegExp(
		(
			// Delimiters.
			"(\\" + strDelimiter + "|\\r?\\n|\\r|^)" +
			// Quoted fields.
			"(?:\"([^\"]*(?:\"\"[^\"]*)*)\"|" +
			// Standard fields.
			"([^\"\\" + strDelimiter + "\\r\\n]*))"
		),
		"gi"
		);
	// Create an array to hold our data. Give the array a default empty first row.
	var arrData = [[]];
	// Create an array to hold our individual pattern matching groups.
	var arrMatches = null;
	// Keep looping over the regular expression matches until we can no longer find a match.
	while (arrMatches = objPattern.exec( strData )){
		// Get the delimiter that was found.
		var strMatchedDelimiter = arrMatches[ 1 ];
		// Check to see if the given delimiter has a length (is not the start of string) and if it matches
		// field delimiter. If id does not, then we know that this delimiter is a row delimiter.
		if (
			strMatchedDelimiter.length &&
			(strMatchedDelimiter != strDelimiter)
			){
			// Since we have reached a new row of data, add an empty row to our data array.
			arrData.push( [] );
		}
		// Now that we have our delimiter out of the way, let's check to see which kind of value we
		// captured (quoted or unquoted).
		if (arrMatches[ 2 ]){
			// We found a quoted value. When we capture this value, unescape any double quotes.
			var strMatchedValue = arrMatches[ 2 ].replace(
				new RegExp( "\"\"", "g" ),
				"\""
				);
		} else {
			// We found a non-quoted value.
			var strMatchedValue = arrMatches[ 3 ];
		}
		// Now that we have our value string, let's add
		// it to the data array.
		arrData[ arrData.length - 1 ].push( strMatchedValue );
	}
	// Return the parsed data.
	return( arrData );
}

function LoadSettings(_uri){
	PopulateLocalSettingsVariablesFromCSV(_uri); // update local variables to from settings file
	CreateSettingsFileCSV(defaultSettingsFileURI); // save local variables to csv
}

function pad(n, width, z) {
  z = z || '0';
  n = n + '';
  return n.length >= width ? n : new Array(width - n.length + 1).join(z) + n;
}

function XmlBasicString(){
	// Dynamic code to generate settings file string
	var settingsToCSV = "			var settingsToCSV='';";
	for (i in settingsDict){
		settingsToCSV+='\n' + "			settingsToCSV+='" + i + ",' + fl.xmlui.get(\'"+i+"\') + '\\n' ;";
	}
	settingsToCSV+='\n' + "			settingsToCSV = settingsToCSV.replace(/\\n$/, '');";
	
	// returns xmlui.accept if settings are modified otherswise xmlui.cancel
	var checkIfSettingsModified = "			if(false ||";
	for (i in settingsDict){
		checkIfSettingsModified+='\n' + "			'"+settingsDict[i]+"' != fl.xmlui.get('"+i+"') ||";
	}	
	checkIfSettingsModified+='\n' + "			false){";
	checkIfSettingsModified+='\n' + "				if(confirm('Keep changes?')){ ";
	checkIfSettingsModified+='\n' +  					settingsToCSV;
	checkIfSettingsModified+='\n' + "					var fileURI = FLfile.platformPathToURI(fl.configDirectory + 'Commands/"+settingsDirectory+"/"+defaultSettingsFileName+"');"
	checkIfSettingsModified+='\n' + "					FLfile.write(fileURI, settingsToCSV);"
	checkIfSettingsModified+='\n' + "					fl.xmlui.accept();";	
	checkIfSettingsModified+='\n' + "				}else{ fl.xmlui.cancel(); }";
	checkIfSettingsModified+='\n' + "			} else {fl.xmlui.cancel();} ";

	var dialogXML = "";
	dialogXML+='\n' + '<dialog id="toolSettings" title="UnimExporter" >'
	dialogXML+='\n' + '	<vbox>'

	dialogXML+='\n' + '		<label value="Sprite Symbol Identifier" />'
	dialogXML+='\n' + '		<hbox>'	
	dialogXML+='\n' + '		<textbox id="SpriteSymbolSubstring" maxlength="100" size="20" multiline="false" value="'+ settingsDict['SpriteSymbolSubstring'] +'" />'
	dialogXML+='\n' + '   	<checkbox id="isExportAnimationData" label="Export animation data" checked="'+settingsDict['isExportAnimationData']+'"/>'
	dialogXML+='\n' + '		</hbox>'
	dialogXML+='\n' + '		<hbox>'	
	dialogXML+='\n' + '   	<checkbox id="isIncludeRGBA" label="Include RGBA" checked="'+settingsDict['isIncludeRGBA']+'"/>'	
	dialogXML+='\n' + '   	<checkbox id="isCompressColorOperations" label="Combine Color Operations" checked="'+settingsDict['isCompressColorOperations']+'"/>'
	dialogXML+='\n' + '   	<checkbox id="isExportSeperatedSprites" label="Export Seperated Sprites" checked="'+settingsDict['isExportSeperatedSprites']+'"/>'
	dialogXML+='\n' + '		</hbox>'

	dialogXML+='\n' + '		<separator/>'

	dialogXML+='\n' + '		<label value="Track Symbol Identifier" />'
	dialogXML+='\n' + '		<hbox>'	
	dialogXML+='\n' + '		<textbox id="TrackSymbolSubstring" maxlength="100" size="20" multiline="false" value="'+ settingsDict['TrackSymbolSubstring'] +'" />'
	dialogXML+='\n' + '   	<checkbox id="isExportTrackData" label="Export Tracking Data" checked="'+settingsDict['isExportTrackData']+'"/>'
	dialogXML+='\n' + '		</hbox>'

	dialogXML+='\n' + '		<separator/>'

	dialogXML+='\n' + '		<label value="Customizations Symbol Identifier   ( Default: Double Underscore )" />'
	dialogXML+='\n' + '		<hbox>'	
	dialogXML+='\n' + '		<textbox id="CustomizationsSymbolSubstring" maxlength="100" size="20" multiline="false" value="'+ settingsDict['CustomizationsSymbolSubstring'] +'" />'
	dialogXML+='\n' + '   	<checkbox id="isExportCustomizationSprites" label="Export Customization Sprites" checked="'+settingsDict['isExportCustomizationSprites']+'"/>'
	dialogXML+='\n' + '		</hbox>'

	dialogXML+='\n' + '		<separator/>'

	dialogXML+='\n' + '		<label value="Clips Layer Identifier" />'
	dialogXML+='\n' + '		<hbox>'	
	dialogXML+='\n' + '		<textbox id="ClipsLayerSubstring" maxlength="100" size="20" multiline="false" value="'+ settingsDict['ClipsLayerSubstring'] +'" />'
	dialogXML+='\n' + '   	<checkbox id="isExportClips" label="Export Clips Data" checked="'+settingsDict['isExportClips']+'"/>'	
	dialogXML+='\n' + '		</hbox>'

	dialogXML+='\n' + '		<separator/>'

	dialogXML+='\n' + '		<label value="Events Layer Identifier" />'
	dialogXML+='\n' + '		<hbox>'	
	dialogXML+='\n' + '		<textbox id="EventsLayerSubstring" maxlength="100" size="20" multiline="false" value="'+ settingsDict['EventsLayerSubstring'] +'" />'
	dialogXML+='\n' + '   	<checkbox id="isExportEvents" label="Export Events" checked="'+settingsDict['isExportEvents']+'"/>'
	dialogXML+='\n' + '		</hbox>'

//////////////////////////////

	dialogXML+='\n' + '		<separator/>'	
	dialogXML+='\n' + '		<spacer></spacer>'	


//////////////////////////////

	dialogXML+='\n' + '   	<hbox>'
	dialogXML+='\n' + '			<button id="mabutton" label="Assign Custom Borders" oncommand="'
	dialogXML+='\n' + "				fl.xmlui.set('runScript','AssignBorder();');" 
	dialogXML+='\n' +  				checkIfSettingsModified;
	dialogXML+='\n' + '			"/>'
	dialogXML+='\n' + '		</hbox>'

//////////////////////////////

	dialogXML+='\n' + '   	<hbox>'
	dialogXML+='\n' + '			<textbox id="BorderPadding" maxlength="4" size="4" multiline="false" value="'+ settingsDict['BorderPadding'] +'" />'
	dialogXML+='\n' + '			<label value="Border Padding (default 2):" />'
	dialogXML+='\n' + '		</hbox>'
	
	dialogXML+='\n' + '   	<hbox>' 
	dialogXML+='\n' + '			<textbox id="ShapePadding" maxlength="4" size="4" multiline="false" value="'+ settingsDict['ShapePadding'] +'" />'
	dialogXML+='\n' + '			<label value="ShapePadding Padding (default 2):" />'
	dialogXML+='\n' + '		</hbox>'
	
	dialogXML+='\n' + '   	<hbox>' 
	dialogXML+='\n' + '			<textbox id="SpriteScale" maxlength="4" size="4" multiline="false" value="'+ settingsDict['SpriteScale'] +'" />'
	dialogXML+='\n' + '			<label value="Sprite scale (default 1):" />'
	dialogXML+='\n' + '		</hbox>'	

	dialogXML+='\n' + '   	<checkbox id="isIncludeHiddenLayers" label="Include Hidden Layers" disabled="true" checked="'+settingsDict['isIncludeHiddenLayers']+'"/>'
	
//////////////////////////////

	dialogXML+='\n' + '		<separator/>'
		
	dialogXML+='\n' + '		<label value="Export Name:" />'
	dialogXML+='\n' + '   	<hbox>'
	dialogXML+='\n' + '			<textbox id="ExportName" maxlength="100" size="20" multiline="false" value="'+ settingsDict['ExportName'] +'" />'
	dialogXML+='\n' + '		</hbox>'
	dialogXML+='\n' + '		<label value="Export Destination:" />'
	dialogXML+='\n' + '   	<hbox>'
	dialogXML+='\n' + '			<textbox id="ExportDestination" maxlength="200" size="80" multiline="false" value="'+ settingsDict['ExportDestination'] +'" />'
	dialogXML+='\n' + '			<button id="mabutton" label="Browse For File" oncommand="'
	dialogXML+='\n' + '				var folderURI = fl.browseForFolderURL(\'open\', \'Select a folder.\');'	
	dialogXML+='\n' + '				if(folderURI) {var folderPath = FLfile.uriToPlatformPath(folderURI); fl.xmlui.set(\'ExportDestination\',folderPath);}'
	dialogXML+='\n' + '				"/>'
	dialogXML+='\n' + '		</hbox>'
	
	dialogXML+='\n' + '		<separator/>'
	
//////////////////////////////
	
	dialogXML+='\n' + '   	<checkbox id="isCompressAnimData" label="Compress animation data as bytes file" checked="'+settingsDict['isCompressAnimData']+'"/>'
	dialogXML+='\n' + '   	<hbox>'
	dialogXML+='\n' + '   	<checkbox id="isMinifyJson" label="Minify JSON" checked="'+settingsDict['isMinifyJson']+'"/>'
	dialogXML+='\n' + '   	<checkbox id="isPrettifyJson" label="Prettify JSON" checked="'+settingsDict['isPrettifyJson']+'"/>'
	dialogXML+='\n' + '   	<checkbox id="isExportAsJsonObj" label="Export as JSON Object Variable" checked="'+settingsDict['isExportAsJsonObj']+'"/>'		
	dialogXML+='\n' + '		</hbox>'
	dialogXML+='\n' + '   	<hbox>' 
	dialogXML+='\n' + '   		<checkbox id="isTruncateDecimal" label="Truncate decimal output" checked="'+settingsDict['isTruncateDecimal']+'"/>'
	dialogXML+='\n' + '			<textbox id="TruncateDecimal" maxlength="2" size="3" multiline="false" value="'+ settingsDict['TruncateDecimal'] +'" />'
	dialogXML+='\n' + '			<label value="Truncation value" />'
	dialogXML+='\n' + '		</hbox>'

	dialogXML+='\n' + '		<separator/>'
	
	dialogXML+='\n' + '		<hbox>'	
	dialogXML+='\n' + '			<button id="runScript" label="EXPORT" oncommand="'
	dialogXML+='\n' + "				if(confirm('Are you absolutely positively sure you want to continue? This process may take a while.')){ "
	dialogXML+='\n' +  					settingsToCSV;
	dialogXML+='\n' + "					var fileURI = FLfile.platformPathToURI(fl.configDirectory + 'Commands/"+settingsDirectory+"/"+defaultSettingsFileName+"');"
	dialogXML+='\n' + "					FLfile.write(fileURI, settingsToCSV);"
	dialogXML+='\n' + "					fl.xmlui.set('runScript','Export();');" 
	dialogXML+='\n' + "					fl.xmlui.accept();}"
	dialogXML+='\n' + '			"/>'
	
	dialogXML+='\n' + '			<button id="mabutton" label="SAVE AS" oncommand="'
	dialogXML+='\n' +  				settingsToCSV;
	dialogXML+='\n' + "				var fileName = prompt('Enter File Name: ');"
	dialogXML+='\n' + "				if (fileName){"
	dialogXML+='\n' + "					var fileURI = FLfile.platformPathToURI(fl.configDirectory + 'Commands/"+settingsDirectory+"/' + fileName + '.csv');"
	dialogXML+='\n' + "					FLfile.write(fileURI, settingsToCSV);"
	dialogXML+='\n' + "				}"
	dialogXML+='\n' + '			"/>'
	
	dialogXML+='\n' + '			<button id="reload" label="LOAD" oncommand="'
	//dialogXML+='\n' + "				prompt('Settings files can be located at', fl.configDirectory+ 'Commands\\\\"+settingsDirectory+"\\\\' );";
	dialogXML+='\n' + '				var settingFileURI = fl.browseForFileURL(\'select\', \'Select SettingsFile.\');'	
	dialogXML+='\n' + '				if(settingFileURI) {'
	dialogXML+='\n' + "					fl.xmlui.set('reload','LoadSettings(\\\''+settingFileURI+'\\\')');"
	dialogXML+='\n' + "				fl.xmlui.accept();}"
	dialogXML+='\n' + '			"/>'
	
	dialogXML+='\n' + '			<button id="mabutton" label="EXIT" oncommand="'
	dialogXML+='\n' + 				checkIfSettingsModified;
	dialogXML+='\n' + '			"/>'
	dialogXML+='\n' + '		</hbox>'
	dialogXML+='\n' + '	</vbox>'
	dialogXML+='\n' + '</dialog>'
	
	return dialogXML;
}


function printSettings()
{
	fl.trace("------------\n");
	for (i in settingsDict)
	{
		fl.trace(i + " : " + settingsDict[i]);
	}
}

function AssignBorder()
{
	var boderName = "_BORDER_";
	var cropBoxScale = 10;
	
	// create border symbol if it doesn't exist
	var isDrawBorderSquare = false;
	if (fl.getDocumentDOM().library.itemExists(boderName)){
		if( confirm(boderName + " symbol already exists. Would you like to replace it with the default border?") ){	
			isDrawBorderSquare = true;
		}
	}
	else{
		fl.getDocumentDOM().library.addNewItem("graphic", boderName);
		isDrawBorderSquare = true;
	}
	if (isDrawBorderSquare){
		fl.getDocumentDOM().library.editItem(boderName);
		
		var stroke = fl.getDocumentDOM().getCustomStroke("toolbar");
		var originalStroke = fl.getDocumentDOM().getCustomStroke("toolbar");

		stroke.color = '#00E800';
		stroke.thickness = 2;
		stroke.scaleType = 'none';
		fl.getDocumentDOM().setCustomStroke(stroke);
		fl.getDocumentDOM().addNewRectangle({left:-50,top:-50,right:50,bottom:50},0, true);

		// reset stroke
		fl.getDocumentDOM().setCustomStroke(originalStroke);
		fl.getDocumentDOM().currentTimeline = 0;
	}
	
	var isReplaceSquare = null;
	if (fl.getDocumentDOM().library.itemExists(boderName)){
		var libraryItem = fl.getDocumentDOM().library.items;
		// loop through each library item
		for (i = 0;  i< libraryItem.length ; i++){
			// if first letters of library item name are '_btmp_' then process it
			if (libraryItem[i].name.indexOf(settingsDict['SpriteSymbolSubstring']) !== -1){
				fl.getDocumentDOM().library.editItem(libraryItem[i].name);
				
				if (fl.getDocumentDOM().getTimeline().layers[0].name == boderName){
					if (isReplaceSquare == null){
						if(confirm(boderName + " layer was found. How would you like to proceed?\nOK   =    Reset all exisiting border layers\nCANCEL   =   Avoid changes to symbols already containing the border layer.")){
							isReplaceSquare = true;
						} 
						else{
							isReplaceSquare = false;
						}
					}
					if (isReplaceSquare){
						fl.getDocumentDOM().getTimeline().deleteLayer(0);
					}
					else{
						continue;
					}
				}
				
				// ADD BORDER LAYER
				fl.getDocumentDOM().getTimeline().currentLayer = 0; //sets to top layer
				fl.getDocumentDOM().getTimeline().addNewLayer(boderName, "normal", true); // make a layer above the tope layer
				fl.getDocumentDOM().getTimeline().currentLayer = 0;	// reselect the top layer.
				
				//	Unlock locked layer
				var lockedLayers = [];
				var lyrs = fl.getDocumentDOM().getTimeline().layers;
				for (var j = 0; j < lyrs.length; j++) {	
					if ( fl.getDocumentDOM().getTimeline().layers[j].locked == true){
						fl.getDocumentDOM().getTimeline().layers[j].locked = false;
						lockedLayers.push(j);
					}
				}
				
				//TODO Show hidden layers
				var cBS = Number(cropBoxScale)/100; //cropBoxScale
				var prevSlctDefined = true;
				var borderTop = 0;
				var borderBottom = 0;
				var borderLeft = 0;
				var borderRight = 0;
				
				var maxFrmLngth = fl.getDocumentDOM().getTimeline().layers[0].frames.length;
				for (var k = 0; k < maxFrmLngth ; k++){
					fl.getDocumentDOM().getTimeline().currentFrame = k;
					fl.getDocumentDOM().getTimeline().setSelectedFrames([0, k, k+1], true);
					
					// convert frame to blank
					if (k != 0){
						//fl.getDocumentDOM().getTimeline().clearKeyframes(k);
						fl.getDocumentDOM().getTimeline().convertToBlankKeyframes(k);
					}
					fl.getDocumentDOM().getTimeline().currentFrame = k;
					fl.getDocumentDOM().getTimeline().setSelectedFrames([0, k, k+1], true);
					fl.getDocumentDOM().selectAll();
					var newRect = fl.getDocumentDOM().getSelectionRect();
					
					// Check if rect selection is valid
					if (newRect.top == undefined){
						if (prevSlctDefined == true){
							borderTop = 0;
							borderBottom = 0;
							borderLeft = 0;
							borderRight = 0;
						}
						else {
							fl.getDocumentDOM().getTimeline().clearKeyframes(k);
						}
						prevSlctDefined = false;
					}
					else{
						// if current selection is similar in size to previous selection then remove the current key frame 
						if(borderTop == newRect.top && borderBottom == newRect.bottom && borderLeft == newRect.left && borderRight == newRect.right){
							fl.getDocumentDOM().getTimeline().clearKeyframes(k);
						}
						else{
							borderTop = newRect.top;
							borderBottom = newRect.bottom;
							borderLeft = newRect.left;
							borderRight = newRect.right;
							for (var b=0 ; b < 20 ; b++){	
								fl.getDocumentDOM().getTimeline().currentFrame = k;
								fl.getDocumentDOM().getTimeline().setSelectedFrames([0, k, k+1], true);
								fl.getDocumentDOM().library.addItemToDocument({x:((borderRight+borderLeft)/2), y:((borderBottom+borderTop)/2)}, boderName);	
								var lmnt = fl.getDocumentDOM().getTimeline().layers[0].frames[k].elements[0];			
								if (lmnt != undefined){ break; }
								else{ DebugLog("Reattempting to add border onto frame"); }
								if (b == 19){
									alert("something went wrong when attempting to add borders. Process will continue");
								}
							}
							fl.getDocumentDOM().getTimeline().currentFrame = k;
							fl.getDocumentDOM().getTimeline().setSelectedFrames([0, k, k+1], true);
							slctn = fl.getDocumentDOM().selection[0];
							slctn.scaleX = Math.abs(borderRight-borderLeft)/100 + cBS;
							slctn.scaleY = Math.abs(borderTop-borderBottom)/100 + cBS;
						}
						prevSlctDefined = true;
					}
				}
				
				// Relock previously locked layers
				for (var j = 0; j < lockedLayers.length; j++) {
					fl.getDocumentDOM().getTimeline().layers[lockedLayers[j]].locked = false;
				}
				
				// TODO ReVisible Layers
			}
		}
	}
	else { alert('Operation cancelled. Could not find '+boderName+' symbol.')}
	fl.getDocumentDOM().currentTimeline = 0;
	return;
}

function RunAntiCrashAlert()
{
	alert("This alert (for some reason) helps AnimateCC not crash. Go ahead and continue.");
}

function DebugLog(text){
	if (isDebugLog){
		fl.trace(text);	
	}
	FLfile.write(debugLogFilePath + logUniqueId + textExtension, text + "\n", "append");
}


function DebugLogWarning(text){
	debugLogWarningText += text + "\n";
}




function JSONstringify(obj)
{
	return _internalStringify(obj, 0);
};

function _internalStringify(obj, depth, fromArray)
{
	var t = typeof (obj);
	if (t != "object" || obj === null)
	{
		// simple data type
		if (t == "string") return '"'+obj+'"';
		return String(obj);
	}
	else
	{
		// recurse array or object
		var n, v, json = [], arr = (obj && obj.constructor == Array);

		var joinString, bracketString, firstPropString;
		if(true)
		{
			joinString = ",\n";
			bracketString = "\n";
			for(var i = 0; i < depth; ++i)
			{
				joinString += "\t";
				bracketString += "\t";
			}
			joinString += "\t";//one extra for the properties of this object
			firstPropString = bracketString + "\t";
		}
		else
		{
			joinString = ",";
			firstPropString = bracketString = "";
		}
		for (n in obj)
		{
			v = obj[n]; t = typeof(v);

			// Ignore functions
			if (t == "function") continue;

			if (t == "string") v = '"'+v+'"';
			else if (t == "object" && v !== null) v = _internalStringify(v, depth + 1, arr);

			json.push((arr ? "" : '"' + n + '":') + String(v));
		}
		return (fromArray || depth === 0 ? "" : bracketString)+ (arr ? "[" : "{") + firstPropString + json.join(joinString) + bracketString + (arr ? "]" : "}");
	}
};

function JSONparse(str)
{
	if (str === "") 
	{
			str = '""';
	}
	eval("var p=" + str + ";"); // jshint ignore:line
	return p;
};

function sleep(milliseconds) 
{
  var start = new Date().getTime();
  for (var i = 0; i < 1e7; i++) 
  {
    if ((new Date().getTime() - start) > milliseconds)
    {
      break;
    }
  }
}
