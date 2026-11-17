using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Xyla
{
 public static class AtlasSpanish
 {
  [Serializable] public class Entry { public string source, name; }
  [Serializable] public class Catalogue { public Entry[] entries; }
  static readonly Dictionary<string,string> names = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<string,string> search = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  static AtlasSpanish()
  {
   var asset=Resources.Load<TextAsset>("AtlasSpanish");
   if(asset==null) throw new InvalidOperationException("Falta el catálogo anatómico en español.");
   foreach(var entry in JsonUtility.FromJson<Catalogue>(asset.text).entries)
   {
    names.Add(entry.source,entry.name);
    string aliases="";
    if(entry.source=="brain") aliases=" cerebro";
    if(entry.source.Contains("scapula")) aliases+=" omoplato";
    if(entry.source.Contains("ulna")) aliases+=" ulna";
    if(entry.source.Contains("fibula")) aliases+=" fibula";
    if(entry.source.Contains("pituitary")) aliases+=" glandula pituitaria";
    if(entry.source.Contains("patella")) aliases+=" patela";
    if(entry.source.Contains("carotid")) aliases+=" carotida";
    search.Add(entry.source,Normalize(entry.name+" "+entry.source+aliases));
   }
  }
  public static string Name(string source)
  {
   if(names.TryGetValue(source,out string translated)) return translated;
   throw new InvalidOperationException("Falta traducir la estructura: "+source);
  }
  public static bool Contains(string source) => names.ContainsKey(source);
  public static bool Matches(string source,string[] normalizedWords)
  {
   string value=search[source];
   foreach(string word in normalizedWords)
    if(value.IndexOf(word,StringComparison.Ordinal)<0)return false;
   return true;
  }
  public static string Normalize(string value)
  {
   var result=new StringBuilder();
   foreach(char c in (value??"").Normalize(NormalizationForm.FormD))
    if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)result.Append(char.ToLowerInvariant(c));
   return result.ToString().Normalize(NormalizationForm.FormC).Trim();
  }
 }
}
