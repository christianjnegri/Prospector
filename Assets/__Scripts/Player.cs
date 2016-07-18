using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Enables LINQ queries, which will be explained soon

// The player can either be human or an ai
public enum PlayerType {
	human,
	ai
}

// The individual player of the game
// Note: Player does NOT extend MonoBehaviour (or any other class)

[System.Serializable] // Make the Player class visible in the Inspector pane
public class Player {
	
	public PlayerType         type = PlayerType.ai;
	public int                playerNum;
	
	public List<CardBartok>   hand; // The cards in this player's hand
	
	public SlotDef            handSlotDef;
	
	// Add a card to the hand
	public CardBartok AddCard(CardBartok eCB) {
		if (hand == null) hand = new List<CardBartok>();
		
		// Add the card to the hand
		hand.Add (eCB);
		
		return( eCB );
	}
	
	// Remove a card from the hand
	public CardBartok RemoveCard(CardBartok cb) {
		hand.Remove(cb);
		return(cb);
	}
	
}
public class Player : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
