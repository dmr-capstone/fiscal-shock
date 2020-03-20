﻿using UnityEngine;

public class SharkScript : MonoBehaviour
{
    //figure out how to import script interfaces
    //Also need threat increase when not paid, gets bad at 5 and really bad at 8
    //This is because the player starts with no debt to the shark
    public static bool sharkDue { get; set; } = false;
    public static float sharkInterestRate { get; set; } = 0.155f;
    public static float sharkMaxLoan { get; set; } = 4000.0f;
    public static int sharkThreatLevel { get; set; } = 3;
    public static float sharkTotal { get; set; }

    public bool addDebt(float amount){
        if (sharkThreatLevel < 5 && sharkMaxLoan > (sharkTotal + amount)){
            //shark threat is below 5 and is below max total debt
            Loan newLoan = new Loan(StateManager.nextID, amount, sharkInterestRate, true);
            StateManager.loanList.AddLast(newLoan);
            PlayerFinance.cashOnHand += amount;
            StateManager.nextID++;
            StateManager.totalLoans++;
            return true;
        } else {
            return false;
        }
    }

    public bool payDebt(float amount, int loanNum){
        if (PlayerFinance.cashOnHand < amount){//amount is more than money on hand
            return false;
        } else if (sharkTotal <= amount){ //amount is more than the debt
            foreach (Loan item in StateManager.loanList)
            {
                if(item.ID == loanNum){
                    StateManager.loanList.Remove(item); //reduce debt to 0 and money on hand by the debt's value
                }
            }
            PlayerFinance.cashOnHand -= sharkTotal;
            sharkDue = false;
            StateManager.totalLoans--;
            return true;
        } else { //none of the above
            //reduce debt and money by amount
            foreach (Loan item in StateManager.loanList)
            {
                if(item.ID == loanNum){
                    item.total -= amount;
                }
            }
            PlayerFinance.cashOnHand -= amount;
            sharkDue = false;
            return true;
        }
    }

    public static void sharkUnpaid()
    {
        bool paid = true;
        foreach (Loan item in StateManager.loanList)
        {
            if(!item.paid && item.source)
            {
                sharkThreatLevel++;
                StateManager.paymentStreak = 0;
                paid = false;
            }
        }
        if(paid){
            StateManager.paymentStreak++;
        }
    }

    public static void sharkInterest()
    {
        float tempTot = 0.0f, tempAdd;
        foreach (Loan item in StateManager.loanList)
        {
            if(item.source)
            {
                item.paid = false;
                tempAdd = item.total * item.rate;
                item.total += tempAdd;
                tempTot += item.total;
            }
        }
        sharkTotal = tempTot;
    }
}
