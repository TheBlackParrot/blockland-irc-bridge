// parts of code taken from RTB4 and Port's RTB4 IRC

if(!isObject(IRCTCPSet))
	new SimSet(IRCTCPSet);

$IRC::InitialRoomName = "BL" @ getNumKeyID() @ "-" @ getSubStr(sha1($Server::Name),0,5);
$IRC::Site = "example.com";
$IRC::Port = 6667;
$IRC::Room = filterString(strReplace($IRC::InitialRoomName," ","_"),"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789[]-");
$IRC::Version = "v0.0.7-1";

package IRCPackage {
	function GameConnection::autoAdminCheck(%client) {
		%client.TCP = %client.connectToIRC();
		messageClient(%client,'',"\c6The IRC addon is connected to\c3" SPC $IRC::Site @ ":" @ $IRC::Port SPC "\c6in room\c3" SPC $IRC::Room);

		return parent::autoAdminCheck(%client);
	}

	function GameConnection::connectToIRC(%client) {
		%TCP = new TCPObject(IRCTCP) {
			site = $IRC::Site;
			port = $IRC::Port;
			connected = 0;
			client = %client;
			// randomness in case multiple clients share the same name
			playerName = getSubStr(%client.name,0,16) @ "-BL" @ getSubStr(sha1(getRandom(99999)),0,5);
		};
		IRCTCPSet.add(%TCP);
		%TCP.connect($IRC::Site @ ":" @ $IRC::Port);

		return %TCP;
	}

	function serverCmdMessageSent(%client,%msg) {
		parent::serverCmdMessageSent(%client,%msg);

		if(%client.TCP.connected) {
			%client.sentMsg = 1;
			%client.TCP.sendLine("PRIVMSG #" @ $IRC::Room SPC ":" @ %msg);
		}
	}

	function GameConnection::onClientLeaveGame(%client) {
		%client.TCP.disconnect();
		%client.TCP.delete();

		return parent::onClientLeaveGame(%client);
	}

	function onServerDestroyed(%a,%b,%c) {
		for(%i=0;%i<IRCTCPSet.getCount();%i++) {
			%TCP = IRCTCPSet.getObject(%i);
			if(%TCP.connected)
				%TCP.disconnect();
			%TCP.delete();
		}

		return parent::onServerDestroyed(%a,%b,%c);
	}
};
activatePackage(IRCPackage);

function IRCTCP::onLine(%this,%line) {
	if($IRC::Debug) {
		echo(%line);
	}

	if(getSubStr(%line,0,1+strLen($IRC::Site)) $= ":" @ $IRC::Site) {
		return;
	}
	if(getWord(%line,0) $= "PING") {
		%this.sendLine("PONG" SPC %this.playerName);
		return;
	}
	switch$(getWord(%line,1)) {
		case "PART" or "QUIT":
			%name = getSubStr(%line,1,striPos(%line,"!")-1);
			messageClient(%this.client,'',"\c4" @ %name SPC "has left the chat");
			return;

		case "JOIN":
			%name = getSubStr(%line,1,striPos(%line,"!")-1);
			for(%i=0;%i<IRCTCPSet.getCount();%i++) {
				%TCP = IRCTCPSet.getObject(%i);

				if(%name $= %TCP.playerName) {
					return;
				}
			}
			messageClient(%this.client,'',"\c4" @ %name SPC "has connected to the chat from IRC");
			return;

		case "NOTICE":
			messageClient(%this.client,'',"\c5IRC Server\c6: " @ stripMLControlChars(getWords(%line,3,getWordCount(%line))));
			return;
	}
	if(getWord(%line,1) $= "PRIVMSG") {
		%name = getSubStr(%line,1,striPos(%line,"!")-1);
		for(%i=0;%i<IRCTCPSet.getCount();%i++) {
			%TCP = IRCTCPSet.getObject(%i);

			if(%name $= %TCP.playerName) {
				return;
			}
		}
		%message = stripMLControlChars(getSubStr(getWords(%line,3),1,strLen(getWords(%line,3))));
		if(strReplace(%message," ","") $= "") {
			return;
		}
		if(getWord(%message,0) $= "ACTION") {
			%message = "<color:00ff00>*" @ getWords(%message,1,getWordCount(%message)) @ "*";
		}
	}

	if(%name $= "" && %message $= "") {
		return;
	}

	messageClient(%this.client,'',"\c4" @ %name @ "\c6:" SPC %message);
}

function IRCTCP::onConnected(%this) {
	echo("Connected");
	%this.connected = 1;
	%name = filterString(strReplace(%this.playerName," ","_"),"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789[]-");
	%this.sendLine("NICK" SPC %name);
	%this.sendLine("USER" SPC %name SPC "0 * :" @ %name @ "-" @ %this.client.bl_id);
	%this.schedule(3000,sendLine,"JOIN #" @ $IRC::Room);
}

function IRCTCP::sendLine(%this,%line) {
	%this.send(%line @ "\r\n");
}

function filterString(%string,%allowed) {
	for(%i=0;%i<strLen(%string);%i++) {
		%char = getSubStr(%string,%i,1);
		if(strPos(%allowed,%char) >= 0) {
			%return = %return @ %char;
		}
	}
	return %return;
}
