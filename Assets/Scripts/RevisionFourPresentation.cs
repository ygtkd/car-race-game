using UnityEngine;
using CoastRacer.Core;
namespace CoastRacer {
 public sealed partial class RaceGame {
  void CreateBridgeDecks(){
   var parent=new GameObject("Overpass structure").transform;parent.SetParent(world.transform,false);
   for(int i=0;i<Track.Samples;i++){
    if(!track.ForeignRoadBelow(i,35))continue;
    var a=V(track.points[i]);var b=V(track.points[(i+1)%Track.Samples]);
    var slab=Shape("Bridge underside",PrimitiveType.Cube,(a+b)*.5f-Vector3.up*.5f,new Vector3(Track.GrassEdge*2,.65f,Vector3.Distance(a,b)+.12f),steel,parent);
    slab.transform.localRotation=Quaternion.LookRotation(b-a);
   }
  }
 }
}