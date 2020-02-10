//by Nick Rewkowski 2/6/2020
//some code to make it easier for Unity people to follow the abstracted UE4 examples
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//equivalent to EAttachmentRule in UE4
public enum AttachmentRule{KeepRelative,KeepWorld,SnapToTarget}


public class NickVRPawn : MonoBehaviour
{
    public GameObject leftPointerObject;
    public GameObject rightPointerObject;
    public TextMesh outputText;
    public TextMesh outputText2;
    public TextMesh outputText3;
    //you can also create event handlers like UE4.... but OVRInput makes this somewhat harder so I'm doing it FSM/condition style

    //aka a bullet prefab... doesn't yet exist in the scene so I need to provide a template/prefab
    //Since bullet prefab has a Bullet script, I can ID it that way instead of generic prefab
    public Bullet definitionOfABullet;

    public LayerMask collectiblesMask;

    GameObject thingOnGun;

    Collectible thingIGrabbed;

    Vector3 previousPointerPos; //using this for velocity since Unity's broken physics engine won't give it to me otherwise

    // Update is called once per frame
    void Update()
    {
        //Assets/Oculus/VR/Scripts.OVRInput.cs has the button definitions
        //this helps as well https://developer.oculus.com/documentation/unity/unity-ovrinput/?locale=en_US
        //Note that the things labelled as "Axis1D" such as triggers can be treated like axes (e.g. input 0-1.0) or like buttons (up or down)

        //Equivalent to InputAction ShootGun in UE4 version
        //I'm doing a bunch of else ifs to make it impossible to hit multiple buttons at once
        if (OVRInput.GetDown(OVRInput.RawButton.LIndexTrigger)){
            outputText.text="Shoot";
            if (!thingOnGun){
                //Quaternion.lookat functions basically the same way as UE4's MakeRotFromX
                Bullet spawnedBullet=Instantiate(definitionOfABullet,leftPointerObject.transform.position,Quaternion.LookRotation(leftPointerObject.transform.up));
                //you should do a nullity check to make sure it actually has a rigidbody, since in Unity, it's possible for a thing to NOT have physics at all, which is not true for UE4
                spawnedBullet.GetComponent<Rigidbody>().AddForce(leftPointerObject.transform.up*100000);
            }
            else{
                detachGameObject(thingOnGun,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld);
                simulatePhysics(thingOnGun,Vector3.zero,true);
                thingOnGun.GetComponent<Rigidbody>().AddForce(leftPointerObject.transform.up*1000);
                thingOnGun=null;
            }
            
        }else if (OVRInput.GetUp(OVRInput.RawButton.LIndexTrigger)) {
            outputText.text="Release";
        }
        
        //Equivalent to InputAction GoToThere in UE4 version
        else if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger)){
            outputText3.text="Teleport";
            RaycastHit outHit;
            if (Physics.Raycast(rightPointerObject.transform.position, rightPointerObject.transform.up, out outHit, 100.0f))
            {
                //you can only see these rays when you leave Game window and look at Scene Window. Can't draw rays in-game like in UE4 unless you create your own Ray prefab
                Debug.DrawRay(rightPointerObject.transform.position, rightPointerObject.transform.up * outHit.distance, Color.red,10000);
                outputText2.text="hit ="+outHit.collider.gameObject;
                //remember that Unity is Y-up, so I need to swap the axes
                NavMeshHit navMeshHit;
                NavMesh.SamplePosition(new Vector3(outHit.point.x,this.gameObject.transform.position.y,outHit.point.z),out navMeshHit,10,NavMesh.AllAreas);
                this.gameObject.transform.position=new Vector3(navMeshHit.position.x,this.gameObject.transform.position.y,navMeshHit.position.z);
            }

        //equivalent to GrabRight in UE4 version (right grip)    
        }else if (OVRInput.GetDown(OVRInput.RawButton.RHandTrigger)){
            outputText3.text="Grip";
            //In Unity, I can't directly get the overlapping actors of a component. I need to query it manually with Physics.OverlapSphere or OnTriggerEnter
            //I overlap with 1 cm radius to try to get only things near hand
            //this will also return collider for the hand mesh if there is one. I disabled it but keep it in mind. You need to make sure hand is on a different layer
            //collectiblesMask is defined at the top right of the Inspector where it says Layer. The layer controls which things to hit (there is no "class filter" like in UE4)
            Collider[] overlappingThings=Physics.OverlapSphere(rightPointerObject.transform.position,0.01f,collectiblesMask);
            if (overlappingThings.Length>0){
                attachGameObjectToAChildGameObject(overlappingThings[0].gameObject,rightPointerObject,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld,true);
                //I'm not bothering to check for nullity because layer mask should ensure I only collect collectibles.
                thingIGrabbed=overlappingThings[0].gameObject.GetComponent<Collectible>();
            }
        }else if (OVRInput.GetUp(OVRInput.RawButton.RHandTrigger) || OVRInput.GetUp(OVRInput.RawButton.A) || OVRInput.GetUp(OVRInput.RawButton.B) || OVRInput.GetUp(OVRInput.RawButton.RThumbstick)){
            letGo();

        //since you can't merge paths the way I did in BP, I need to create a function that does the force grab thing or else I would duplicate code
        //equivalent to ShootAndGrabNoSnap in UE4 version (A)
        }else if (OVRInput.GetDown(OVRInput.RawButton.A)){
            outputText3.text="Force Grab at Distance";
            forceGrab(true);

        //equivalent to ShootAndGrabSnap in UE4 version (B)
        }else if (OVRInput.GetDown(OVRInput.RawButton.B)){
            outputText3.text="Force Grab Snap";
            forceGrab(false);

        //equivalent to MagneticGrip in UE4 version (RS/R3)
        }else if (OVRInput.GetDown(OVRInput.RawButton.RThumbstick)){
            outputText3.text="Magnetic Grip";
            Collider[] overlappingThings=Physics.OverlapSphere(rightPointerObject.transform.position,1,collectiblesMask);
            if (overlappingThings.Length>0){
                Collectible nearestCollectible=getClosestHitObject(overlappingThings);
                attachGameObjectToAChildGameObject(nearestCollectible.gameObject,rightPointerObject,AttachmentRule.SnapToTarget,AttachmentRule.SnapToTarget,AttachmentRule.KeepWorld,true);
                //I'm not bothering to check for nullity because layer mask should ensure I only collect collectibles.
                thingIGrabbed=nearestCollectible.gameObject.GetComponent<Collectible>();
            }
        }
        previousPointerPos=rightPointerObject.gameObject.transform.position;
    }

    Collectible getClosestHitObject(Collider[] hits){
        float closestDistance=10000.0f;
        Collectible closestObjectSoFar=null;
        foreach (Collider hit in hits){
            Collectible c=hit.gameObject.GetComponent<Collectible>();
            if (c){
                float distanceBetweenHandAndObject=(c.gameObject.transform.position-rightPointerObject.gameObject.transform.position).magnitude;
                if (distanceBetweenHandAndObject<closestDistance){
                    closestDistance=distanceBetweenHandAndObject;
                    closestObjectSoFar=c;
                }
            }
        }
        return closestObjectSoFar;
    }
    //could have more easily just passed in attachment rule.... but I wanted to keep the code similar to the BP example
    void forceGrab(bool pressedA){
        RaycastHit outHit;
        //notice I'm using the layer mask again
        if (Physics.Raycast(rightPointerObject.transform.position, rightPointerObject.transform.up, out outHit, 100.0f,collectiblesMask))
        {
            AttachmentRule howToAttach=pressedA?AttachmentRule.KeepWorld:AttachmentRule.SnapToTarget;
            attachGameObjectToAChildGameObject(outHit.collider.gameObject,rightPointerObject.gameObject,howToAttach,howToAttach,AttachmentRule.KeepWorld,true);
            thingIGrabbed=outHit.collider.gameObject.GetComponent<Collectible>();
        }
    }

    void letGo(){
        if (thingIGrabbed){
            Collider[] overlappingThingsWithLeftHand=Physics.OverlapSphere(leftPointerObject.transform.position,0.01f,collectiblesMask);
            if (overlappingThingsWithLeftHand.Length>0){
                if (thingOnGun){
                    detachGameObject(thingOnGun,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld);
                    simulatePhysics(thingOnGun,Vector3.zero,true);
                }
                attachGameObjectToAChildGameObject(overlappingThingsWithLeftHand[0].gameObject,leftPointerObject,AttachmentRule.SnapToTarget,AttachmentRule.SnapToTarget,AttachmentRule.KeepWorld,true);
                thingOnGun=overlappingThingsWithLeftHand[0].gameObject;
                thingIGrabbed=null;
            }else{
                detachGameObject(thingIGrabbed.gameObject,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld,AttachmentRule.KeepWorld);
                simulatePhysics(thingIGrabbed.gameObject,(rightPointerObject.gameObject.transform.position-previousPointerPos)/Time.deltaTime,true);
                thingIGrabbed=null;
            }
        }
    }
    
    //since Unity doesn't have sceneComponents like UE4, we can only attach GOs to other GOs which are children of another GO
    //e.g. attach collectible to controller GO, which is a child of VRRoot GO
    //imagine if scenecomponents in UE4 were all split into distinct GOs in Unity
    public void attachGameObjectToAChildGameObject(GameObject GOToAttach, GameObject newParent, AttachmentRule locationRule, AttachmentRule rotationRule, AttachmentRule scaleRule, bool weld){
        GOToAttach.transform.parent=newParent.transform;
        handleAttachmentRules(GOToAttach,locationRule,rotationRule,scaleRule);
        if (weld){
            simulatePhysics(GOToAttach,Vector3.zero,false);
        }
    }

    public static void detachGameObject(GameObject GOToDetach, AttachmentRule locationRule, AttachmentRule rotationRule, AttachmentRule scaleRule){
        //making the parent null sets its parent to the world origin (meaning relative & global transforms become the same)
        GOToDetach.transform.parent=null;
        handleAttachmentRules(GOToDetach,locationRule,rotationRule,scaleRule);
    }

    public static void handleAttachmentRules(GameObject GOToHandle, AttachmentRule locationRule, AttachmentRule rotationRule, AttachmentRule scaleRule){
        GOToHandle.transform.localPosition=
        (locationRule==AttachmentRule.KeepRelative)?GOToHandle.transform.position:
        //technically don't need to change anything but I wanted to compress into ternary
        (locationRule==AttachmentRule.KeepWorld)?GOToHandle.transform.localPosition:
        new Vector3(0,0,0);

        //localRotation in Unity is actually a Quaternion, so we need to specifically ask for Euler angles
        GOToHandle.transform.localEulerAngles=
        (rotationRule==AttachmentRule.KeepRelative)?GOToHandle.transform.eulerAngles:
        //technically don't need to change anything but I wanted to compress into ternary
        (rotationRule==AttachmentRule.KeepWorld)?GOToHandle.transform.localEulerAngles:
        new Vector3(0,0,0);

        GOToHandle.transform.localScale=
        (scaleRule==AttachmentRule.KeepRelative)?GOToHandle.transform.lossyScale:
        //technically don't need to change anything but I wanted to compress into ternary
        (scaleRule==AttachmentRule.KeepWorld)?GOToHandle.transform.localScale:
        new Vector3(1,1,1);
    }

    public void simulatePhysics(GameObject target,Vector3 oldParentVelocity,bool simulate){
        Rigidbody rb=target.GetComponent<Rigidbody>();
        if (rb){
            if (!simulate){
                Destroy(rb);
            } 
        } else{
            if (simulate){
                //there's actually a problem here relative to the UE4 version since Unity doesn't have this simple "simulate physics" option
                //The object will NOT preserve momentum when you throw it like in UE4.
                //need to set its velocity itself.... even if you switch the kinematic/gravity settings around instead of deleting/adding rb
                Rigidbody newRB=target.AddComponent<Rigidbody>();
                newRB.velocity=oldParentVelocity;
            }
        }
    }
}
