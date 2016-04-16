﻿using System;
using UnityPlatformer.Characters;

namespace UnityPlatformer.Monitors {
  public class CharacterMonitor : ControllerMonitor {

    Character character;

    override public void Start() {
      base.Start ();
      character = GetComponent<Character> ();
    }
    override public  void OnGUI() {
      base.OnGUI ();
    }

    override public void Update() {
      base.Update ();
      text += string.Format(
        "Area: {0}\n"+
        "State: {1}\n"+
        "Ladder: {2} IsAboveTop {3} IsBelowBottom {4}\n" +
        "Platform: {5}\n",
        character.area.ToString(),
        character.state.ToString(),
        character.ladder,
        character.ladder ? character.ladder.IsAboveTop(character) : false,
        character.ladder ? character.ladder.IsBelowBottom(character) : false,
        character.platform
      );
    }
  }
}