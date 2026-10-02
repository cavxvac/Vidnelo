using System;
using System.IO;
using LinkDownloader;
class UpdateChecks {
    static string Json(string tag,string url,bool prerelease=false) {
        return "{\"tag_name\":\""+tag+"\",\"html_url\":\""+url+"\",\"draft\":false,\"prerelease\":"+(prerelease?"true":"false")+",\"body\":\"Release notes\"}";
    }
    static void Require(bool ok,string message) {if(!ok)throw new Exception(message);}
    static void Main() {
        string url=AppUpdates.Repository+"/releases/tag/v1.1";
        Require(AppUpdates.Parse(Json("v1.0",url)).Version==new Version(1,0,0,0),"Normalize 1.0");
        Require(AppUpdates.Parse(Json("v1.10",url)).Version>new Version(1,9,0,0),"Numeric order");
        Require(AppUpdates.Parse(Json("v0.9",url)).Version<new Version(1,0,0,0),"No downgrade");
        Require(AppUpdates.Parse(Json("v1.1",url,true))==null,"Skip prerelease");
        Require(AppUpdates.Parse(Json("v1.1",url).Replace("\"draft\":false","\"draft\":true"))==null,"Skip drafts");
        foreach(string bad in new[]{"https://example.com/download","http://github.com/cavxvac/Vidnelo/releases/tag/v1.1","https://github.com/other/repo/releases/tag/v1.1","https://github.com:123/cavxvac/Vidnelo/releases/tag/v1.1"}) {
            bool rejected=false;try {AppUpdates.Parse(Json("v1.1",bad));}catch(InvalidDataException){rejected=true;}Require(rejected,"Reject foreign URL");
        }
        bool invalid=false;try {AppUpdates.Parse(Json("v1.1-beta",url));}catch(InvalidDataException){invalid=true;}Require(invalid,"Reject malformed version");
        Console.WriteLine("PASS version normalization, numeric comparison, no downgrade, prerelease/draft filtering and release URL validation");
    }
}
