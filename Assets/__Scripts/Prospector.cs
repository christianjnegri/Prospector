using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public enum ScoreEvent {
	draw,
	mine,
	mineGold,
	gameWin,
	gameLoss
}

public class Prospector : MonoBehaviour {

	static public Prospector 	S;
	static public int SCORE_FROM_PREV_ROUND = 0;
	static public int HIGH_SCORE = 0;
	public float                reloadDelay = 1f; // The delay between rounds
	public Vector3              fsPosMid  = new Vector3(0.5f, 0.90f, 0);
	public Vector3              fsPosRun  = new Vector3(0.5f, 0.75f, 0);
	public Vector3              fsPosMid2 = new Vector3(0.5f, 0.5f,  0);
	public Vector3              fsPosEnd  = new Vector3(1.0f, 0.65f, 0);
	public Deck					deck;
	public TextAsset			deckXML;
	public Layout layout;
	public TextAsset layoutXML;
	public Vector3 layoutCenter;
	public float xOffset = 3;
	public float yOffset = -2.5f;
	public Transform layoutAnchor;
	public CardProspector target;
	public List<CardProspector> tableau;
	public List<CardProspector> discardPile;


	void Awake(){
		S = this;
		if (PlayerPrefs.HasKey ("ProspectorHighScore")) {
			HIGH_SCORE = PlayerPrefs.GetInt ("ProspectorHighScore");
		}
		score += SCORE_FROM_PREV_ROUND;
		SCORE_FROM_PREV_ROUND = 0;

		// Set up the GUITexts that show at the end of the round
		// Get the GUIText Components
		GameObject go = GameObject.Find ("GameOver");
		if (go != null) {
			GTGameOver = go.GetComponent<GUIText>();
		}
		go = GameObject.Find ("RoundResult");
		if (go != null) {
			GTRoundResult = go.GetComponent<GUIText>();
		}
		// Make them invisible
		ShowResultGTs(false);
		
		go = GameObject.Find("HighScore");
		string hScore = "High Score: "+Utils.AddCommasToNumber(HIGH_SCORE);
		go.GetComponent<GUIText>().text = hScore;
	}
	
	void ShowResultGTs(bool show) {
		GTGameOver.gameObject.SetActive(show);
		GTRoundResult.gameObject.SetActive(show);
	}
	
	
	public List<CardProspector> drawPile;
	
	public int chain = 0;
	public int scoreRun = 0;
	public int score = 0;
	public FloatingScore        fsRun;

	public GUIText                GTGameOver;
	public GUIText                GTRoundResult;
	
	void Start() {
		Scoreboard.S.score = score;
		deck = GetComponent<Deck> ();
		deck.InitDeck (deckXML.text);
		Deck.Shuffle (ref deck.cards);
		layout = GetComponent<Layout> ();
		layout.ReadLayout (layoutXML.text);
		drawPile = ConvertListCardsToListCardProspectors (deck.cards);
		LayoutGame ();
	}

	CardProspector Draw() {
		CardProspector cd = drawPile [0];
		drawPile.RemoveAt (0);
		return(cd);
	}

	CardProspector FindCardByLayoutID(int layoutID){
		foreach (CardProspector tCP in tableau) {
			if (tCP.layoutID == layoutID) {
				return(tCP);
			}
		}
		return(null);
	}

	void LayoutGame(){
		if (layoutAnchor == null) {
			GameObject tGO = new GameObject ("_LayoutAnchor");
			layoutAnchor = tGO.transform;
			layoutAnchor.transform.position = layoutCenter;
		}

		CardProspector cp;

		foreach (SlotDef tSD in layout.slotDefs) {
			cp = Draw ();
			cp.faceUp = tSD.faceUp;
			cp.transform.parent = layoutAnchor;
			cp.transform.localPosition = new Vector3 (layout.multiplier.x * tSD.x, layout.multiplier.y * tSD.y, -tSD.layerID);
			cp.layoutID = tSD.id;
			cp.slotDef = tSD;
			cp.state = CardState.tableau;
			cp.SetSortingLayerName( tSD.layerName); // Set the sorting layers
			tableau.Add (cp);
		}
		foreach (CardProspector tCP in tableau) {
			foreach (int hid in tCP.slotDef.hiddenBy) {
				cp = FindCardByLayoutID (hid);
				tCP.hiddenBy.Add (cp);
			}
		}
		MoveToTarget (Draw ());
		UpdateDrawPile ();
	}
	List<CardProspector> ConvertListCardsToListCardProspectors(List<Card>lCD){
		List<CardProspector>lCP = new List<CardProspector>();
		CardProspector tCP;
		foreach(Card tCD in lCD) {
			tCP = tCD as CardProspector;
			lCP.Add (tCP);
		}
		return(lCP);
	}
	public void CardClicked( CardProspector cd) { 
		// The reaction is determined by the state of the clicked card 
		switch (cd.state) { 
		case CardState.target: 
			// Clicking the target card does nothing 
			break; 
		case CardState.drawpile: 
			// Clicking any card in the drawPile will draw the next card 
			MoveToDiscard(target); // Moves the target to the discardPile 
			MoveToTarget(Draw()); // Moves the next drawn card to the target 
			UpdateDrawPile(); // Restacks the drawPile 
			ScoreManager(ScoreEvent.draw);
			break; 
		case CardState.tableau: // Clicking a card in the tableau will check if it's a valid play 
			bool validMatch = true; 
			if (!cd.faceUp) { 
				// If the card is face-down, it's not valid 
				validMatch = false; 
			} 
			if (!AdjacentRank(cd, target)) { 
				// If it's not an adjacent rank, it's not valid 
				validMatch = false; 
			} 
			if (!validMatch) return; // return if not valid 
			// Yay! It's a valid card. 
			tableau.Remove(cd); // Remove it from the tableau List 
			MoveToTarget(cd); // Make it the target card
			SetTableauFaces();
			ScoreManager(ScoreEvent.mine);
			break; 
		} 
		CheckForGameOver ();
	}		
	void MoveToDiscard( CardProspector cd) { 
		// Set the state of the card to discard 
		cd.state = CardState.discard; 
		discardPile.Add( cd); // Add it to the discardPile List < > 
		cd.transform.parent = layoutAnchor; // Update its transform parent 
		cd.transform.localPosition = new Vector3( layout.multiplier.x * layout.discardPile.x, layout.multiplier.y * layout.discardPile.y, -layout.discardPile.layerID + 0.5f ); 
		// ^ Position it on the discardPile 
		cd.faceUp = true; // Place it on top of the pile for depth sorting 
		cd.SetSortingLayerName( layout.discardPile.layerName); 
		cd.SetSortOrder(-100 + discardPile.Count); 
	}
	// Make cd the new target card  
	void MoveToTarget( CardProspector cd) { 
		// If there is currently a target card, move it to discardPile 
		if (target != null) MoveToDiscard(target); 
		target = cd; // cd is the new target 
		cd.state = CardState.target; 
		cd.transform.parent = layoutAnchor; 
		// Move to the target position 
		cd.transform.localPosition = new Vector3( layout.multiplier.x * layout.discardPile.x, layout.multiplier.y * layout.discardPile.y, -layout.discardPile.layerID ); 
		cd.faceUp = true; // Make it face-up 
		// Set the depth sorting 
		cd.SetSortingLayerName( layout.discardPile.layerName); 
		cd.SetSortOrder( 0); 
	} 
	// Arranges all the cards of the drawPile to show how many are left 
	void UpdateDrawPile() { 
		CardProspector cd; 
		// Go through all the cards of the drawPile 
		for (int i = 0; i < drawPile.Count; i ++) { 
			cd = drawPile[ i];
			cd.transform.parent = layoutAnchor; 
			// Position it correctly with the layout.drawPile.stagger 
			Vector2 dpStagger = layout.drawPile.stagger; 
			cd.transform.localPosition = new Vector3( layout.multiplier.x * (layout.drawPile.x + i* dpStagger.x), layout.multiplier.y * (layout.drawPile.y + i* dpStagger.y), -layout.drawPile.layerID + 0.1f* i ); 
			cd.faceUp = false; // Make them all face-down 
			cd.state = CardState.drawpile; // Set depth sorting 
			cd.SetSortingLayerName(layout.drawPile.layerName); 
			cd.SetSortOrder(-10* i); 
		}
	}
	public bool AdjacentRank(CardProspector c0, CardProspector c1) {
		if (!c0.faceUp || !c1.faceUp)
			return(false);

		if (Mathf.Abs (c0.rank - c1.rank) == 1) {
			return (true);
		}
		if (c0.rank == 13 & c1.rank == 1)
			return(true);
		if (c0.rank == 1 & c1.rank == 13)
			return(true);

		return(false);
	}
	void SetTableauFaces(){
		foreach (CardProspector cd in tableau) {
			bool fup = true;
			foreach (CardProspector cover in cd.hiddenBy) {
				if (cover.state == CardState.tableau) {
					fup = false;
				}
				cd.faceUp = fup;
			}
		}
	}
	void CheckForGameOver() {
		if(tableau.Count==0){
			GameOver(true);
			return;
		}
		if(drawPile.Count>0){
			return;
		}
		foreach (CardProspector cd in tableau) {
			if (AdjacentRank (cd, target)) {
				return;
			}
		}
		GameOver(false);
	}

	void GameOver(bool won){
		if (won) {
			ScoreManager(ScoreEvent.gameWin);
		} else {
			ScoreManager(ScoreEvent.gameLoss);
		}
		// Reload the scene in reloadDelay seconds
		// This will give the score a moment to travel
		Invoke ("ReloadLevel", reloadDelay);                                 //1
		// Application.LoadLevel("__Prospector_Scene_0"); // Now commented out
	}
	
	void ReloadLevel() {
		// Reload the scene, resetting the game
		Application.LoadLevel("__Prospector_Scene_0");
	}
	void ScoreManager(ScoreEvent sEvt){
		
		List<Vector3> fsPts;
		
		switch (sEvt) {
		case ScoreEvent.gameWin:
			//won round
		case ScoreEvent.gameLoss:
			//lost round
			chain = 0;
			score += scoreRun;
			scoreRun = 0;
			if (fsRun != null) {
				// Create points for the Bezier curve
				fsPts = new List<Vector3>();
				fsPts.Add( fsPosRun );
				fsPts.Add( fsPosMid2 );
				fsPts.Add( fsPosEnd );
				fsRun.reportFinishTo = Scoreboard.S.gameObject;
				fsRun.Init(fsPts, 0, 1);
				// Also adjust the fontSize
				fsRun.fontSizes = new List<float>(new float[] {28,36,4});
				fsRun = null; // Clear fsRun so it's created again
			}
			break;
		case ScoreEvent.mine:
			chain++;
			scoreRun += chain;
			// Create a FloatingScore for this score
			FloatingScore fs;
			// Move it from the mousePosition to fsPosRun
			Vector3 p0 = Input.mousePosition;
			p0.x /= Screen.width;
			p0.y /= Screen.height;
			fsPts = new List<Vector3>();
			fsPts.Add( p0 );
			fsPts.Add( fsPosMid );
			fsPts.Add( fsPosRun );
			fs = Scoreboard.S.CreateFloatingScore(chain,fsPts);
			fs.fontSizes = new List<float>(new float[] {4,50,28});
			if (fsRun == null) {
				fsRun = fs;
				fsRun.reportFinishTo = null;
			} else {
				fs.reportFinishTo = fsRun.gameObject;
			}
			break;
		}
		switch (sEvt) {
		case ScoreEvent.gameWin:
			GTGameOver.text = "Round Over";
			Prospector.SCORE_FROM_PREV_ROUND = score;
			print ("You won this round! Round score: " + score);
			GTRoundResult.text = "You won this round!\nRound Score: "+score;
			ShowResultGTs (true);
			break;
		case ScoreEvent.gameLoss:
			GTGameOver.text = "Game Over";
			if (Prospector.HIGH_SCORE <= score) {
				print ("You got the high score! High score: " + score);
				string sRR = "You got the high score!\nHigh score: "+score;
				GTRoundResult.text = sRR;
				Prospector.HIGH_SCORE = score;

				PlayerPrefs.SetInt ("ProspectorHighScore", score);
			} else {
				print ("Your final score for the game was: " + score);
				GTRoundResult.text = "Your final score was: " + score;
			}
			ShowResultGTs (true);
			break;
		default:
			print ("score: " + score + " scoreRun: " + scoreRun + " chain: " + chain);
			break;
		}
	}
}