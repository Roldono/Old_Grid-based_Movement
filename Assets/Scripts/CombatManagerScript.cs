using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatManagerScript : MonoBehaviour
{
    //This script is for the battle system that this game uses

    public GameManagerScript GMS;

    private bool battleStatus;

    //IN: two 'unit' game objects the initiator is the unit that initiated the attack and the recipient is the receiver
    //OUT: void - units take damage or are destroyed if the hp threshold is <=
    //DESC: This is usually called by another script which has access to the two units and then just sets the units as parameters for the function

    public void Batlle(GameObject initiator, GameObject recipient)
    {
        battleStatus = true;

        var initiatorUnit = initiator.GetComponent<Unit>();
        var recipientUnit = recipient.GetComponent<Unit>();
        int initiatorAttackDamage = initiatorUnit.attackDamage;

        recipientUnit.TakeDamage(initiatorAttackDamage);
        if (CheckIfDead(recipient))
        {
            recipient.transform.parent = null;
            recipientUnit.UnitDeath();
            battleStatus = false;
            GMS.CheckIfUnitsRemain(initiator, recipient);
            return;
        }
            
        battleStatus = false;
    }

    //IN: gameObject to check
    //OUT: boolean - true if unit is dead, false
    //DESC: the health of the gameobject is checked (must be 'unit') or it'll break
    public bool CheckIfDead(GameObject unitToCheck)
    {
        if (unitToCheck.GetComponent<Unit>().currentHealth <= 0) 
        {
            return true;
        }
        return false;
    }
}
