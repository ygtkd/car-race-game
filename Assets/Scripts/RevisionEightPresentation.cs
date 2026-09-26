using UnityEngine;
namespace CoastRacer {
 public sealed partial class RaceGame {
  float onlineSceneTime;Transform scenicTrain;GameObject sceneryOwner;
  void UpdateRevisionEightScenery(float dt){
   if(showroom||world==null)return;
   if(sceneryOwner!=world){sceneryOwner=world;scenicTrain=null;foreach(var t in world.GetComponentsInChildren<Transform>())if(t.name=="train"){scenicTrain=t;break;}}
   if(scenicTrain&&cars.Count>0){float seconds=online?onlineSceneTime:session.time;scenicTrain.localPosition=new Vector3(-440+Mathf.Sin(seconds*.045f)*135,11,365);}
  }
 }
}
