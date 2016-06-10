using System;
using System.Collections.Generic;
using UnityEngine;
using UnityPlatformer;

namespace UnityPlatformer {
  /// <summary>
  /// Base class for: Players, NPCs, enemies.
  /// Handle all movement logic and transform collider information
  /// into 'readable' information for animations.
  /// </summary>
  [RequireComponent (typeof (PlatformerCollider2D))]
  [RequireComponent (typeof (CharacterHealth))]
  public class Character: MonoBehaviour, IUpdateEntity {
    #region public

    [Comment("Time to wait before change state to falling.")]
    public float fallingTime = 0.1f;
    [Comment("Time before disable OnGround State.")]
    public float groundGraceTime = 0.05f;

    public float minVelocity = 0.05f;

    ///
    /// Callbacks
    ///

    public delegate void AreaChange(Areas before, Areas after);
    public AreaChange onAreaChange;
    public delegate void HurtCharacter(DamageType dt, Character to);
    public HurtCharacter onHurtCharacter;
    public delegate void StateChange(States before, States after);
    public StateChange onStateChange;
    public delegate void CharacterDelegate(Character character, float delta);
    public CharacterDelegate onBeforeMove;
    public CharacterDelegate onAfterMove;

    #endregion

    #region ~private

    [HideInInspector]
    public List<CharacterAction> actions = new List<CharacterAction>();
    [HideInInspector]
    public States state = States.None;
    [HideInInspector]
    public Areas area = Areas.None;
    [HideInInspector]
    public BoxCollider2D body;
    [HideInInspector]
    public Ladder ladder;
    [HideInInspector]
    public Liquid liquid;
    [HideInInspector]
    public Grab grab;
    [HideInInspector]
    public Vector2 lastJumpDistance {
      get {
        return jumpEnd - jumpStart;
      }
    }
    public Vector2 lastFallDistance {
      get {
        return fallEnd - fallStart;
      }
    }
    [HideInInspector]
    public Vector2 fallDistance;
    [HideInInspector]
    public MovingPlatform platform;
    [HideInInspector]
    public Vector3 velocity;
    [HideInInspector]
    public PlatformerCollider2D pc2d;
    [HideInInspector]
    public CharacterHealth health;

    #endregion

    #region private

    CharacterAction lastAction;

    Cooldown fallingCD;
    Cooldown groundCD;

    Vector2 jumpStart;
    Vector2 jumpEnd;

    Vector2 fallStart;
    Vector2 fallEnd;

    #endregion

    /// <summary>
    /// This method precalculate some vars, but those value could change. This need to be refactored.
    /// Maybe setters are the appropiate method to refactor this.
    /// </summary>
    virtual public void Awake() {
      //Debug.Log("Start new Character: " + gameObject.name);
      pc2d = GetComponent<PlatformerCollider2D> ();
      health = GetComponent<CharacterHealth>();
      body = GetComponent<BoxCollider2D>();

      fallingCD = new Cooldown(fallingTime);
      groundCD = new Cooldown(groundGraceTime);

      health.onDeath += OnDeath;
    }

    public void Attach(UpdateManager um) {
    }

    /// <summary>
    /// Managed update called by UpdateManager
    /// Transform Input into platformer magic :)
    /// </summary>
    public virtual void ManagedUpdate(float delta) {
      int prio = 0;
      int tmp;
      CharacterAction action = null;

      foreach (var i in actions) {
        tmp = i.WantsToUpdate(delta);
        if (tmp < 0) {
          i.PerformAction(Time.fixedDeltaTime);
        } else if (prio < tmp) {
          prio = tmp;
          action = i;
        }
      }

      // reset / defaults
      pc2d.disableWorldCollisions = false;
      PostUpdateActions a = PostUpdateActions.WORLD_COLLISIONS | PostUpdateActions.APPLY_GRAVITY;

      if (action != null) {
        if (lastAction != action) {
          action.GainControl(delta);
        }

        action.PerformAction(Time.fixedDeltaTime);
        a = action.GetPostUpdateActions();
      }

      if (Utils.biton((int)a, (int)PostUpdateActions.APPLY_GRAVITY)) {
        // TODO REVIEW x/y gravity...
        velocity.y += pc2d.gravity.y * delta;
      }

      if (!Utils.biton((int)a, (int)PostUpdateActions.WORLD_COLLISIONS)) {
        pc2d.disableWorldCollisions = true;
      }

      if (Mathf.Abs(velocity.x) < minVelocity) {
        velocity.x = 0.0f;
      }

      if (Mathf.Abs(velocity.y) < minVelocity) {
        velocity.y = 0.0f;
      }

      if (onBeforeMove != null) {
        onBeforeMove(this, delta);
      }

      pc2d.Move(velocity * delta);

      if (onAfterMove != null) {
        onAfterMove(this, delta);
      }


      // this is meant to fix jump and falling hit something unexpected
      if (pc2d.collisions.above || pc2d.collisions.below) {
        velocity.y = 0;
      }

      if (pc2d.collisions.below) {
        fallingCD.Reset();
        groundCD.Reset();
        SolfEnterState(States.OnGround);
      } else {
        groundCD.Increment();
        // give some margin
        if (groundCD.Ready()) {
          SolfExitState(States.OnGround);
        }

        // falling but not wallsliding
        if (velocity.y < 0 && !IsOnState(States.WallSliding)) {
          fallingCD.Increment();
          if (fallingCD.Ready()) {
            SolfEnterState(States.Falling);
          }
        }
      }

      if (lastAction != null && lastAction != action) {
        lastAction.LoseControl(delta);
      }

      lastAction = action;
    }

    public bool IsOnState(States _state) {
      return (state & _state) == _state;
    }

    public bool IsOnArea(Areas _area) {
      return (area & _area) == _area;
    }

    /// <summary>
    /// EnterState if it's not already in it
    /// It a safe mechanism to not trigger the change
    /// </summary>
    public void SolfEnterState(States a) {
      if (!IsOnState(a)) {
        EnterState(a);
      }
    }
    /// <summary>
    /// EnterState if it's not already in it
    /// It a safe mechanism to not trigger the change
    /// </summary>
    public void SolfExitState(States a) {
      if (IsOnState(a)) {
        ExitState(a);
      }
    }
    /// <summary>
    /// Notify that Character enter a new state.
    /// There are incompatible states, like jumping and falling,
    /// You don't need to handle those manually, we will exit
    /// any state needed here.
    /// </summary>
    /// <param name="a">State to enter.</param>
    /// <param name="privcall">Do call callbacks, changes will be batch into one call.</param>
    public void EnterState(States a, bool privcall = false) {
      if (a == States.Falling && IsOnState(States.Falling)) {
        fallStart = transform.position;
      }
      if (a == States.Jumping && !IsOnState(States.Jumping)) {
        jumpStart = transform.position;
        // temporary disable slopes because jump initial velocity.y
        // was modified by ClimbSlope & DescendSlope
        pc2d.DisableSlopes(0.1f);
      }

      if (a == States.Falling) {
        ExitState(States.Hanging, true);
      }
      if (a == States.Jumping) {
        ExitState(States.Falling | States.OnGround | States.WallSliding, true);
      }
      if (a == States.OnGround) {
        ExitState(States.Falling | States.Hanging | States.WallSliding, true);
      }
      if (a == States.WallSliding) {
        ExitState(States.Falling | States.Hanging | States.Jumping, true);
      }
      // while grabbing or ladder, just do that!
      if (a == States.Grabbing || a == States.Ladder) {
        state = 0;
      }
      if (state == States.Grabbing) {
        // while grabbing cannot enter in another state
        // we need to leave first
        return;
      }

      States before = state;
      state |= a;

      if (!privcall && onStateChange != null && before != state) {
        onStateChange(before, state);
      }
    }

    /// <summary>
    /// Notify that Character enter a new state.
    /// There are incompatible states, like jumping and falling,
    /// You don't need to handle those manually, we will exit
    /// any state needed here.
    /// </summary>
    /// <param name="a">State to enter.</param>
    /// <param name="privcall">Do call callbacks, changes will be batch into one call.</param>
    public void ExitState(States a, bool privcall = false) {
      // TODO REVIEW if Hanging include Jumping this fail...
      if (a == States.Falling && IsOnState(States.Falling)) {
        fallEnd = transform.position;
      }
      if (a == States.Jumping && IsOnState(States.Jumping)) {
        jumpEnd = transform.position;
      }

      States before = state;
      state &= ~a;

      if (!privcall && onStateChange != null && before != state) {
        onStateChange(before, state);
      }
    }

    public void EnterArea(Areas a) {
      Areas before = area;
      area |= a;

      if (before != area && onAreaChange != null) {
        onAreaChange(before, area);
      }
    }

    public void ExitArea(Areas a) {
      Areas before = area;
      area &= ~a;

      if (before != area && onAreaChange != null) {
        onAreaChange(before, area);
      }
    }

    virtual public void OnDeath() {
      Debug.Log("Player die! play some fancy animation!");
    }

    public virtual Vector3 GetFeetPosition() {
      return body.bounds.center - new Vector3(
        0,
        body.bounds.size.y * 0.5f,
        0);
    }

    public virtual Vector3 GetCenter() {
      return pc2d.GetComponent<BoxCollider2D>().bounds.center;
    }

    public virtual void OnEnable() {
      UpdateManager.instance.characters.Add(this);
    }

    public virtual void OnDisable() {
      UpdateManager.instance.characters.Remove(this);
    }
  }
}
