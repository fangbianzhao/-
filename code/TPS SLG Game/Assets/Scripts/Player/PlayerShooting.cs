﻿using UnityEngine;
using Newtonsoft.Json;

public class PlayerShooting : MonoBehaviour
{
    public int damagePerShot = 20;
    public float fire1CoolingTime = 0.15f;
    public float fire2Coolingtime = GameSettings.MISSILE_SHOOT_INTERVAL;
    public float range = 100f;
    public GameObject shootingPivot;


    float fire1Timer;
    float fire2Timer;
    Ray shootRay = new Ray();
    RaycastHit shootHit;
    int shootableMask;
    ParticleSystem gunParticles;
    LineRenderer gunLine;
    AudioSource gunAudio;
    Light gunLight;
    float effectsDisplayTime = 0.2f;
    Transform player;

    private NetworkHost networkHost;


    void Awake ()//initialize status
    {
        shootableMask = LayerMask.GetMask ("Shootable");
        gunParticles = GetComponent<ParticleSystem> ();//Add gun particle component
        gunLine = GetComponent <LineRenderer> ();//Add gun line component
        gunAudio = GetComponent<AudioSource> ();//Add audio component
        gunLight = GetComponent<Light> ();//Add light component
        player = GetComponentInParent<Transform>();

        networkHost = NetworkHost.GetInstance();
        fire1Timer = fire1CoolingTime;
        fire2Timer = fire2Coolingtime;
    }

    //Been called per frame
    void Update ()
    {
        fire1Timer += Time.deltaTime;//Clock for shoot1
        fire2Timer += Time.deltaTime;//Clock for missle
        MissileCoolingControl.missileCoolingTime = fire2Timer;//send cooling message

        if (PlayerInput.GetFire1() && fire1Timer >= fire1CoolingTime && Time.timeScale != 0)
        {
            Shoot1 ();
        }

        if(fire1Timer >= fire1CoolingTime * effectsDisplayTime)
        {
            DisableEffects ();
        }


        if (PlayerInput.GetFire2() && fire2Timer >= fire2Coolingtime && Time.timeScale != 0)
        {
            Shoot2();
        }
    }

    void Shoot2()//Logic for shoot2
    {
        fire2Timer = 0f;
        //SendMissileShootMsg();

        shootRay.origin = this.shootingPivot.transform.position;
        shootRay.direction = this.shootingPivot.transform.forward;
        Vector3 entPoint;

        if (Physics.Raycast(shootRay, out shootHit, range, shootableMask))
        {

            entPoint = shootHit.point;
        }
        else
        {
            entPoint = shootRay.origin + shootRay.direction * range;
        }

        Vector3 towardVector = entPoint - transform.position;

        SendMissileShootMsg(towardVector);
    }

    void SendMissileShootMsg(Vector3 towardVector)//Logic for missle shooting
    {
        ClientMissileShootMsg clientMissileShootMsg = new ClientMissileShootMsg
        {
            playerID = GameSettings.playerID
        };
        clientMissileShootMsg.location.SetLocation(transform.position);
        clientMissileShootMsg.rotation.SetRotation(transform.eulerAngles);

        //Vector3 zVector = new Vector3(0, 0, 1);
        //Vector3 towardVector = Quaternion.Euler(transform.eulerAngles) * zVector;

        clientMissileShootMsg.towardVector.SetLocation(towardVector.normalized);

        string missileShootMsgJson = JsonConvert.SerializeObject(clientMissileShootMsg);

        byte[] msg = MessageHandler.SetClientMsg(
           NetworkSettings.MISSILE_ENTITY_SERVICE_ID,
           NetworkSettings.MISSILE_ENTITY_SHOOT_CMD,
           missileShootMsgJson);

        StartCoroutine(networkHost.SendBytesMessage(msg));
    }


    public void DisableEffects ()//Disable player shooting movement
    {
        gunLine.enabled = false;
        gunLight.enabled = false;
    }


    void Shoot1 ()//Logic for shoot 1
    {
        fire1Timer = 0f;//Shoot 1 cooling time is 0

        gunAudio.Play ();

        gunLight.enabled = true;

        gunParticles.Stop ();
        gunParticles.Play ();

        gunLine.enabled = true;
        gunLine.SetPosition (0, transform.position);

        shootRay.origin = this.shootingPivot.transform.position;
        shootRay.direction = this.shootingPivot.transform.forward;

        if(Physics.Raycast (shootRay, out shootHit, range, shootableMask))
        {   
            EnemyHealth enemyHealth = shootHit.collider.GetComponent <EnemyHealth> ();
            if(enemyHealth != null && !enemyHealth.isDead)
            {
                // send change enemy health msg
                SendPlayerAttackMsg(damagePerShot, enemyHealth.monsterID, shootHit.point);
                enemyHealth.TakeDamage(damagePerShot, shootHit.point);
            }
            else
            {
                SendPlayerAttackMsg(damagePerShot, -1, shootHit.point);
            }
            gunLine.SetPosition (1, shootHit.point);
        }
        else
        {
            gunLine.SetPosition (1, shootRay.origin + shootRay.direction * range);
            SendPlayerAttackMsg(damagePerShot, -1, shootRay.origin + shootRay.direction * range);
        }
    }

    void SendPlayerAttackMsg(int damage, int monsterID, Vector3 shootPoint)//send player attact message to server
    {
        ClientPlayerAttackMsg clientPlayerAttackMsg = new ClientPlayerAttackMsg
        {
            monsterID = monsterID,
            playerDamage = damage,
            shootPoint = new Location(shootPoint)
        };

        string playerAttackMsgJson = JsonConvert.SerializeObject(clientPlayerAttackMsg);

        byte[] msg = MessageHandler.SetClientMsg(
           NetworkSettings.PLAYER_ENTITY_SERVICE_ID,
           NetworkSettings.PLAYER_ENTITY_ATTACK_CMD,
           playerAttackMsgJson);

        StartCoroutine(networkHost.SendBytesMessage(msg));
    }
}
