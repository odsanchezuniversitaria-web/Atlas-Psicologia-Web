mergeInto(LibraryManager.library,{
 AWReady:function(receiver){if(window.AtlasApp)window.AtlasApp.ready(UTF8ToString(receiver));},
 AWState:function(json){if(window.AtlasApp)window.AtlasApp.receive(JSON.parse(UTF8ToString(json)));},
 AWHaptic:function(strength,seconds){if(window.AtlasApp)window.AtlasApp.haptic(strength,seconds);},
 AWCheckpoint:function(json){if(window.AtlasApp)window.AtlasApp.save(JSON.parse(UTF8ToString(json)));},
 AWVoiceOpen:function(receiver,config){if(window.AtlasVoice)window.AtlasVoice.open(UTF8ToString(receiver),JSON.parse(UTF8ToString(config)));},
 AWVoiceClose:function(receiver){if(window.AtlasVoice)window.AtlasVoice.close(UTF8ToString(receiver));},
 AWVoiceSend:function(receiver,payload){if(window.AtlasVoice)window.AtlasVoice.send(UTF8ToString(receiver),JSON.parse(UTF8ToString(payload)));},
 AWVoiceRpc:function(receiver,id,payload,error){if(window.AtlasVoice)window.AtlasVoice.resolve(UTF8ToString(receiver),UTF8ToString(id),UTF8ToString(payload),!!error);},
 AWVoiceMute:function(receiver,muted){if(window.AtlasVoice)window.AtlasVoice.mute(UTF8ToString(receiver),!!muted);}
});

