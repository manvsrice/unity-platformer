using System;
using UnityEngine;

namespace UnityPlatformer {
  /// <summary>
  /// Movement while on ground and not slipping
  /// </summary>
  public class CharacterActionGroundMovement: CharacterAction {
    #region public

    [Comment("Movement speed")]
    public float speed = 6;
    [Comment("Time to reach max speed")]
    public float accelerationTime = .1f;

    #endregion

    float velocityXSmoothing;

    /// <summary>
    /// Execute when collision below.
    /// </summary>
    public override int WantsToUpdate(float delta) {
      // NOTE if Air/Ground are very different maybe:
      // if (pc2d.IsOnGround(<frames>)) it's better
      if (pc2d.collisions.below &&
        !character.IsOnState(States.Slipping) &&
        !character.IsOnState(States.Pushing)) {
        return -1;
      }
      return 0;
    }

    /// <summary>
    /// Reset SmoothDamp
    /// </summary>
    public override void GainControl(float delta) {
      base.GainControl(delta);
      velocityXSmoothing = 0;
    }


    /// <summary>
    /// Do horizontal movement
    /// </summary>
    public override void PerformAction(float delta) {
      Move(speed, ref velocityXSmoothing, accelerationTime);
    }

    /// <summary>
    /// Horizontal movement based on current input
    /// </summary>
    public void Move(float spdy, ref float smoothing, float accTime) {
      float targetVelocityX = input.GetAxisRawX() * spdy;

      character.velocity.x = Mathf.SmoothDamp (
        character.velocity.x,
        targetVelocityX,
        ref velocityXSmoothing,
        accTime
      );
    }

    public override PostUpdateActions GetPostUpdateActions() {
      return PostUpdateActions.WORLD_COLLISIONS | PostUpdateActions.APPLY_GRAVITY;
    }
  }
}
