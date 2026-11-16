using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShowOfHands.Tests {
 public class FlowTests {
  static object Invoke(App app,string method,params object[] args)=>typeof(App).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,args);
  [UnityTest] public IEnumerator OfflineOpeningGuessRecordKeepAndMenus(){
   var camera=new GameObject("Test camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();var go=new GameObject("Flow test");var app=go.AddComponent<App>();
   float until=Time.realtimeSinceStartup+10;while(app.Screen=="Loading"&&Time.realtimeSinceStartup<until)yield return null;
   Assert.That(app.Screen,Is.Not.EqualTo("Loading"));Assert.That(app.Screen,Is.Not.EqualTo("Content missing"));
   Invoke(app,"Home");yield return new WaitForSeconds(.35f);CheckBounds();Capture(camera,"editor-home.png");
   Invoke(app,"Opening");Assert.That(app.Screen,Is.EqualTo("Wave hello"));
   Invoke(app,"Guess");yield return new WaitForSeconds(.35f);CheckBounds();Capture(camera,"editor-guess.png");
   var recorded=(HandClip)typeof(App).GetField("recorded",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
   var cards=UnityEngine.Object.FindObjectsByType<Card>(FindObjectsSortMode.None);Card right=null;foreach(var card in cards)if(card.Text.Text==recorded.Word)right=card;Assert.That(right,Is.Not.Null);
   Invoke(app,"Answer",recorded.Word,right);yield return new WaitForSeconds(1.6f);Assert.That(app.Screen,Is.EqualTo("Get ready"));
   yield return new WaitForSeconds(11.5f);Assert.That(app.Screen,Is.EqualTo("Your performance"));
   Invoke(app,"Keep");Assert.That(app.Screen,Is.EqualTo("Kept"));
   foreach(var name in new[]{"Home","Today","Settings","MoreSettings","Gloves","Mine"}){Invoke(app,name);yield return null;CheckBounds();}
   UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(camera.gameObject);
  }
  static void Capture(Camera camera,string name){var target=new RenderTexture(1280,960,24);camera.targetTexture=target;camera.aspect=4f/3f;camera.fieldOfView=65;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../Logs/"+name),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);}
  static void CheckBounds(){foreach(var card in UnityEngine.Object.FindObjectsByType<Card>(FindObjectsSortMode.None)){if(!card.gameObject.activeInHierarchy)continue;var p=card.transform.localPosition;var h=card.Target.HalfSize;foreach(float x in new[]{p.x-h.x,p.x+h.x})Assert.That(Mathf.Abs(Mathf.Atan2(x,p.z)*Mathf.Rad2Deg),Is.LessThanOrEqualTo(30),card.name);foreach(float y in new[]{p.y-h.y,p.y+h.y})Assert.That(Mathf.Abs(Mathf.Atan2(y,p.z)*Mathf.Rad2Deg),Is.LessThanOrEqualTo(25),card.name);}}
 }
}
