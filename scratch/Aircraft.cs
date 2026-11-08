using System;
using System.Collections.Generic;
using System.Linq;
using AircraftStates;
using UniRx;
using UnityEngine;

namespace SeaPower;

public class Aircraft : ObjectBase
{
	public DamageModel.AircraftWingSide _damagedWingSide;

	public bool engineOn;

	private double _startTime;

	private double _stopTime;

	private bool audioStopped;

	private bool propsSwitched;

	private bool engineStarted;

	private bool engineStopped;

	private float mainThrustPercent;

	private bool _collidersEnabled;

	private Vector3 _bombPassAimError;

	private double _bombPassAimErrorTime = double.NegativeInfinity;

	private Vector3 _bombPassAimTarget;

	private const float BOMB_PASS_WINDOW_SECONDS = 5f;

	public const bool TossBombingEnabled = false;

	private Vector3 _bombPassUnitError;

	private Vector3 _bombPassTrack;

	private Vector3 _bombPassTargetPos;

	private ObjectBase _bombPassTarget;

	private AmmunitionParameters _bombPassAp;

	private bool _bombPassDive;

	private bool _bombPassAimOffsetCommitted;

	private Vector3 _bombPassAimOffset;

	private double _bombPassAimOffsetStamp = double.NegativeInfinity;

	public int _telegraph;

	private NoMotionController state_noMotionController;

	private Default state_default;

	public SelectMovementType _state_selectMovementType;

	public MissileEvasion _state_missileEvasion;

	private EvadeMissile _state_defaultEvasion;

	private AdvancedMissileEvasion _state_advancedMissileEvasion;

	private readonly HashSet<Missile> _ignoredMissiles = new HashSet<Missile>();

	public bool _isInFlight = true;

	public bool _checkForWinchester = true;

	public bool _resetFormation;

	private bool _collisionNoticed;

	public bool _avoidingTerrain;

	private bool _opticalAircraftVisualsHidden;

	private readonly Dictionary<GameObject, bool> _opticalHiddenGameObjectStates = new Dictionary<GameObject, bool>();

	private readonly Dictionary<TrailRenderer, bool> _opticalHiddenTrailRendererStates = new Dictionary<TrailRenderer, bool>();

	private readonly Dictionary<LineRenderer, bool> _opticalHiddenLineRendererStates = new Dictionary<LineRenderer, bool>();

	public bool _inDiveAttackMode;

	public bool _inLevelAttackMode;

	public bool _tossBombRequested;

	public bool _tossBombClimbStarted;

	public float _tossBombAltitude;

	public float _tossBombHoldAltitude;

	public Missile _missileToEvade;

	private List<(ParticleSystem system, ParticleSystem.MinMaxGradient startColor)> _exhaustBaseColors;

	public const float CollisionMissDistanceMeters = 60f;

	private const float COLLISION_ROLL_IN_SECONDS = 0.8f;

	public AircraftAnimation AircraftAnimation { get; set; }

	public AircraftFlightControlSystem Afcs { get; set; }

	public AircraftParameters Ap
	{
		get
		{
			return (AircraftParameters)_obp;
		}
		private set
		{
			_obp = value;
		}
	}

	public bool EngineStarted
	{
		get
		{
			return engineStarted;
		}
		set
		{
			engineStarted = value;
			if (engineStarted)
			{
				_startTime = GameTime.missionElapsedTime;
			}
		}
	}

	public bool EngineStopped
	{
		get
		{
			return engineStopped;
		}
		set
		{
			engineStopped = value;
			if (engineStopped)
			{
				_stopTime = GameTime.missionElapsedTime;
			}
		}
	}

	public ReactiveProperty<float> EnduranceInSec { get; } = new ReactiveProperty<float>();

	public ReactiveProperty<float> FlightTimeLeftInSec { get; } = new ReactiveProperty<float>();

	public ReactiveProperty<float> ActualRangeInKm { get; } = new ReactiveProperty<float>();

	public float MachNumber => _velocityInKnots / (Atmosphere.SpeedOfSound(getHeightInMeters()) * 1.94384f);

	public Vector3 BombPassAimOffset
	{
		get
		{
			if (!_bombPassAimOffsetCommitted)
			{
				return Vector3.zero;
			}
			if (_bombPassAimOffsetStamp != GameTime.missionElapsedTime)
			{
				_bombPassAimOffsetStamp = GameTime.missionElapsedTime;
				Vector3 targetPos = ((_bombPassTarget != null && !_bombPassTarget.IsDestroyed) ? _bombPassTarget.transform.position : _bombPassTargetPos);
				float releaseScale;
				float num = BombPassAimSigma(_bombPassAp, _bombPassTarget, targetPos, _bombPassDive, out releaseScale);
				Vector3 vector = new Vector3(_bombPassTrack.z, 0f, 0f - _bombPassTrack.x);
				_bombPassAimOffset = _bombPassTrack * (_bombPassUnitError.x * num * 1.44f) + vector * (_bombPassUnitError.z * num * 0.58f);
			}
			return _bombPassAimOffset;
		}
	}

	public MotionController Motioncontroller { get; protected set; }

	public override float MaxForwardSpeedInKnots
	{
		get
		{
			float maxSpeedSeaLevel = Ap._maxSpeedSeaLevel;
			float b = Atmosphere.SpeedOfSound(getHeightInMeters()) * Ap._machLimit * 1.94384f;
			return Mathf.Lerp(maxSpeedSeaLevel, b, Mathf.Clamp01(getHeightInFeet() / 10000f));
		}
	}

	public ReactiveProperty<float> HeightOverTerrain { get; } = new ReactiveProperty<float>();

	public bool CanUseAIStates
	{
		get
		{
			if (base.IsPlayerObject)
			{
				return Globals.useExperimentalAircraftAI;
			}
			return true;
		}
	}

	public ObjectBase TemporaryFormationLeader { get; private set; }

	public Vector3 TemporaryFormationStationPosition { get; set; }

	public bool HasTemporaryFormationReference
	{
		get
		{
			if (TemporaryFormationLeader != null)
			{
				return !TemporaryFormationLeader.IsDestroyed;
			}
			return false;
		}
	}

	public ObjectBase ActiveFormationLeader
	{
		get
		{
			if (!HasTemporaryFormationReference)
			{
				return base.Formation?.LeaderStation?.UnitObject;
			}
			return TemporaryFormationLeader;
		}
	}

	public Vector3 ActiveFormationStationPosition
	{
		get
		{
			if (!HasTemporaryFormationReference)
			{
				return base.FormationStation?.StationPosition ?? Vector3.zero;
			}
			return TemporaryFormationStationPosition;
		}
	}

	public Vector3 ActiveFormationStationWorldPosition
	{
		get
		{
			ObjectBase activeFormationLeader = ActiveFormationLeader;
			if (activeFormationLeader == null)
			{
				return base.transform.position;
			}
			Vector3 activeFormationStationPosition = ActiveFormationStationPosition;
			Vector3 normalized = Vector3.ProjectOnPlane(activeFormationLeader.transform.forward, Vector3.up).normalized;
			Vector3 normalized2 = Vector3.ProjectOnPlane(activeFormationLeader.transform.right, Vector3.up).normalized;
			return activeFormationLeader.transform.position + normalized2 * activeFormationStationPosition.x + Vector3.up * activeFormationStationPosition.y + normalized * activeFormationStationPosition.z;
		}
	}

	public GeoPosition ActiveFormationStationGeoPosition => Utils.worldPositionFromUnityToLongLat(ActiveFormationStationWorldPosition, Globals._currentCenterTile);

	public float TurnRadiusMeters => Motioncontroller.Velocity * Motioncontroller.Velocity / (9.8f * MathF.Sqrt(Ap._maxG * Ap._maxG - 1f));

	public bool CanDiveBomb()
	{
		if (!Ap._isLarge)
		{
			return !Ap._unitRoles.Contains(ObjectBaseParameters.UnitRoles.HeavyBomber);
		}
		return false;
	}

	public bool CanTossBomb()
	{
		return false;
	}

	public void ClearBombPassAimOffset()
	{
		_bombPassAimOffsetCommitted = false;
		_bombPassAimOffset = Vector3.zero;
		_bombPassTarget = null;
		_bombPassAp = null;
	}

	public void CommitBombPassAimOffset(AmmunitionParameters ap, ObjectBase target, Vector3 targetPos, bool divePass)
	{
		Vector3 bombPassAimOffset = BombPassAimOffset;
		ClearBombPassAimOffset();
		if (ap != null && ap._type == Ammunition.Type.Bomb && ap._guidanceType == AmmunitionParameters.GuidanceType.None)
		{
			targetPos -= bombPassAimOffset;
			Vector3 unityVelocityVector = base.UnityVelocityVector;
			unityVelocityVector.y = 0f;
			_bombPassTrack = ((unityVelocityVector.sqrMagnitude > 1E-06f) ? unityVelocityVector.normalized : new Vector3(base.transform.forward.x, 0f, base.transform.forward.z).normalized);
			_bombPassUnitError = GetBombPassAimError(targetPos);
			_bombPassTargetPos = targetPos;
			_bombPassTarget = target;
			_bombPassAp = ap;
			_bombPassDive = divePass;
			_bombPassAimOffsetStamp = double.NegativeInfinity;
			_bombPassAimOffsetCommitted = true;
		}
	}

	public float BombPassAimSigma(AmmunitionParameters ap, ObjectBase target, Vector3 targetPos, bool divePass, out float releaseScale)
	{
		float num = Mathf.Max(targetPos.y, 0f);
		float num2 = Mathf.Max(base.transform.position.y - num, 0.1f);
		float num6;
		if (divePass)
		{
			float f = 0.34906584f;
			float num3 = Mathf.Max(_velocityInUnity * 67.200066f, 1f);
			float num4 = num3 * Mathf.Sin(f);
			float num5 = (0f - num4 + Mathf.Sqrt(num4 * num4 + 2f * ap._gravity * num2 * 67.200066f)) / ap._gravity;
			num6 = num3 * Mathf.Cos(f) * num5 * 0.0148809375f;
		}
		else
		{
			num6 = Utils.BombDistanceToDrop(base.UnityVelocityVector, base.transform.position.y, num, ap._gravity);
		}
		releaseScale = Mathf.Sqrt(num2 * num2 + num6 * num6) / 81.64278f;
		float aimErrorSigma = Bomb.GetAimErrorSigma(this, targetPos, target);
		if (!Ap._bombAimErrorFixedWithAltitude)
		{
			return Mathf.Min(aimErrorSigma, 16.328556f) * releaseScale;
		}
		return Mathf.Min(aimErrorSigma, 1.7315173f * releaseScale);
	}

	public Vector3 GetBombPassAimError(Vector3 targetPos)
	{
		if (!(GameTime.missionElapsedTime - _bombPassAimErrorTime < 5.0) || !((targetPos - _bombPassAimTarget).sqrMagnitude < 9f))
		{
			_bombPassAimError = new Vector3(Utils.sampleGauss(Globals._rnd) * 3f, 0f, Utils.sampleGauss(Globals._rnd) * 3f);
			_bombPassAimTarget = targetPos;
		}
		_bombPassAimErrorTime = GameTime.missionElapsedTime;
		return _bombPassAimError;
	}

	private void initStates()
	{
		base.StateMachine = new StateMachine();
		state_noMotionController = new NoMotionController(this, -1);
		state_default = new Default(this, 10);
		At(state_noMotionController, state_default, () => Motioncontroller != null && _isInFlight);
		Destroyed to = new Destroyed(this, -2);
		AtAny(to, () => base.IsDestroyed);
		Default obj = new Default(this, -1);
		At(state_noMotionController, obj, () => Motioncontroller != null && !_isInFlight);
		At(state_default, obj, () => !_isInFlight);
		At(obj, state_default, () => _isInFlight);
		MovingInFormation movingInFormation = new MovingInFormation(this, 9);
		At(state_default, movingInFormation, () => InAirFormation());
		At(movingInFormation, state_default, () => !InAirFormation());
		At(movingInFormation, state_default, () => _resetFormation);
		CAP cAP = new CAP(this, 8);
		At(state_default, cAP, () => base.FollowingFormation && base.Formation.GetStationForUnit(this).Role == StationRole.CAP);
		At(cAP, state_default, () => !base.FollowingFormation || base.FormationStation.Role != StationRole.CAP);
		Intercept intercept = new Intercept(this, 8);
		At(state_default, intercept, () => base.FollowingFormation && base.Formation.GetStationForUnit(this).Role == StationRole.Intercept);
		At(intercept, state_default, () => intercept.IsFinished);
		MPA mPA = new MPA(this, 8);
		At(state_default, mPA, () => base.FollowingFormation && base.Formation.GetStationForUnit(this).Role == StationRole.MPA);
		At(mPA, state_default, () => !base.FollowingFormation || base.FormationStation.Role != StationRole.MPA);
		Loitering loitering = new Loitering(this, 9, 150f * Ap._wingSpan);
		At(state_default, loitering, () => base.CourseCommand.Value == null && !base.CommandPosition.HasValue && base.LoiterPosition is GeoPosition);
		At(movingInFormation, loitering, () => base.CourseCommand.Value == null && !base.CommandPosition.HasValue && base.LoiterPosition is GeoPosition);
		At(loitering, state_default, () => base.CourseCommand.Value != null || base.CommandPosition.HasValue || !(base.LoiterPosition is GeoPosition));
		float orbitRadius = 150f * Ap._wingSpan * 0.0148809375f;
		MaritimePatrol maritimePatrol = new MaritimePatrol(this, 8);
		At(state_default, maritimePatrol, () => base.HasControl && Ap._unitRoles.Contains(ObjectBaseParameters.UnitRoles.MPA) && GetDistToCurrentActiveWaypoint() < 1.1f * orbitRadius && Globals.useExperimentalAircraftAI);
		At(movingInFormation, maritimePatrol, () => base.Formation != null && base.FormationStation.Role == StationRole.MPA && GetDistToCurrentActiveWaypoint() < 1.1f * orbitRadius && Globals.useExperimentalAircraftAI);
		At(maritimePatrol, state_default, () => maritimePatrol.IsFinished);
		IdentifySurfaceContact identifySurfaceContact = new IdentifySurfaceContact(this, 6);
		Func<bool> condition = () => _ai._objectToClassify != null && _ai._objectToClassify.GetMapVehicle(base._taskforce) != null && !_ai._objectToClassify.GetMapVehicle(base._taskforce).Identified.Value && (_ai._objectToClassify._type == ObjectType.Vessel || (_ai._objectToClassify._type == ObjectType.Submarine && !_ai._objectToClassify.isSubmerged()));
		Func<bool> condition2 = () => _ai._objectToIdentify != null && _ai._objectToIdentify.GetMapVehicle(base._taskforce) != null && !_ai._objectToIdentify.GetMapVehicle(base._taskforce).Identified.Value && (_ai._objectToIdentify._type == ObjectType.Vessel || (_ai._objectToIdentify._type == ObjectType.Submarine && !_ai._objectToIdentify.isSubmerged()));
		At(state_default, identifySurfaceContact, condition);
		At(state_default, identifySurfaceContact, condition2);
		At(mPA, identifySurfaceContact, condition);
		At(mPA, identifySurfaceContact, condition2);
		At(maritimePatrol, identifySurfaceContact, condition);
		At(maritimePatrol, identifySurfaceContact, condition2);
		At(loitering, identifySurfaceContact, condition);
		At(loitering, identifySurfaceContact, condition2);
		At(identifySurfaceContact, maritimePatrol, () => identifySurfaceContact.IsFinished && _lastBaseState == maritimePatrol);
		At(identifySurfaceContact, state_default, () => identifySurfaceContact.IsFinished && _lastBaseState != maritimePatrol && _lastBaseState != loitering);
		At(identifySurfaceContact, loitering, () => identifySurfaceContact.IsFinished && _lastBaseState == loitering);
		IdentifySubSurfaceContact identifySubSurfaceContact = new IdentifySubSurfaceContact(this, 6);
		Func<bool> condition3 = () => _ai._objectToClassify != null && _ai._objectToClassify.GetMapVehicle(base._taskforce) != null && !_ai._objectToClassify.GetMapVehicle(base._taskforce).Identified.Value && _ai._objectToClassify._type == ObjectType.Submarine && _ai._objectToClassify.isSubmerged();
		Func<bool> condition4 = () => _ai._objectToIdentify != null && _ai._objectToIdentify.GetMapVehicle(base._taskforce) != null && !_ai._objectToIdentify.GetMapVehicle(base._taskforce).Identified.Value && _ai._objectToIdentify._type == ObjectType.Submarine && _ai._objectToIdentify.isSubmerged();
		At(state_default, identifySubSurfaceContact, condition3);
		At(state_default, identifySubSurfaceContact, condition4);
		At(maritimePatrol, identifySubSurfaceContact, condition3);
		At(maritimePatrol, identifySubSurfaceContact, condition4);
		At(loitering, identifySubSurfaceContact, condition3);
		At(loitering, identifySubSurfaceContact, condition4);
		At(identifySubSurfaceContact, maritimePatrol, () => identifySubSurfaceContact.IsFinished && _lastBaseState == maritimePatrol);
		At(identifySubSurfaceContact, state_default, () => identifySubSurfaceContact.IsFinished && _lastBaseState != maritimePatrol && _lastBaseState != loitering);
		At(identifySubSurfaceContact, loitering, () => identifySubSurfaceContact.IsFinished && _lastBaseState == loitering);
		IdentifyAirContact identifyAirContact = new IdentifyAirContact(this, 6);
		Func<bool> condition5 = () => AllowAI() && _ai._objectToClassify != null && _ai._objectToClassify.GetMapVehicle(base._taskforce) != null && !_ai._objectToClassify.GetMapVehicle(base._taskforce).Identified.Value && _ai._objectToClassify.IsAirUnit;
		Func<bool> condition6 = () => AllowAI() && _ai._objectToIdentify != null && _ai._objectToIdentify.GetMapVehicle(base._taskforce) != null && !_ai._objectToIdentify.GetMapVehicle(base._taskforce).Identified.Value && _ai._objectToIdentify.IsAirUnit;
		At(state_default, identifyAirContact, condition5);
		At(state_default, identifyAirContact, condition6);
		At(identifyAirContact, state_default, () => identifyAirContact.IsFinished);
		AirborneEarlyWarningOrbit aewOrbit = new AirborneEarlyWarningOrbit(this, 8, 150f * Ap._wingSpan);
		At(state_default, aewOrbit, () => AllowAI() && base.HasControl && !base.InFormation.Value && Ap._unitRoles.Contains(ObjectBaseParameters.UnitRoles.AEW) && GetNumberOfWaypoints() == 1 && GetDistToCurrentActiveWaypoint() < 1.1f * aewOrbit.orbitRadius * 0.0148809375f && base.Altitude.Value >= Globals._aircraftCapEntryAltitude);
		At(aewOrbit, state_default, () => aewOrbit.IsFinished);
		AirborneEarlyWarningLine airborneEarlyWarningLine = new AirborneEarlyWarningLine(this, 8);
		At(movingInFormation, airborneEarlyWarningLine, () => base.FollowingFormation && base.FormationStation.Role == StationRole.AEW && GetDistToCurrentActiveWaypointInMiles() < 1f && CanUseAIStates);
		At(state_default, airborneEarlyWarningLine, () => base.FollowingFormation && base.FormationStation.Role == StationRole.AEW && GetDistToCurrentActiveWaypointInMiles() < 1f && CanUseAIStates);
		At(airborneEarlyWarningLine, state_default, () => !base.FollowingFormation || base.FormationStation.Role != StationRole.AEW);
		Aligning aligning = new Aligning(this, 4);
		AtAny(aligning, () => base.Aligning && base.StateMachine.CurrentState.Priority > aligning.Priority);
		At(aligning, state_default, () => aligning.IsFinished);
		MoveToEngage moveToEngage = new MoveToEngage(this, 5);
		AtAny(moveToEngage, () => AllowAI() && !base.Aligning && HasMoveToEngageTask() && base.StateMachine.CurrentState.Priority > moveToEngage.Priority);
		At(moveToEngage, state_default, () => moveToEngage.IsFinished);
		EngageSurfaceContact engageSurfaceContact = new EngageSurfaceContact(this, 3);
		Func<bool> condition7 = () => AllowAI() && base.Aligning && _alignmentTasks[_alignmentTasks.Count - 1]._targetObject != null && (_alignmentTasks[_alignmentTasks.Count - 1]._targetObject._type == ObjectType.Vessel || _alignmentTasks[_alignmentTasks.Count - 1]._targetObject._type == ObjectType.LandUnit || (_alignmentTasks[_alignmentTasks.Count - 1]._targetObject._type == ObjectType.Submarine && !_alignmentTasks[_alignmentTasks.Count - 1]._targetObject.isSubmerged())) && base.StateMachine.CurrentState.Priority > engageSurfaceContact.Priority;
		AtAny(engageSurfaceContact, condition7);
		At(engageSurfaceContact, state_default, () => engageSurfaceContact.IsFinished);
		EngageSubSurfaceContact engageSubSurfaceContact = new EngageSubSurfaceContact(this, 3);
		Func<bool> condition8 = () => AllowAI() && base.Aligning && _alignmentTasks[_alignmentTasks.Count - 1]._targetObject != null && _alignmentTasks[_alignmentTasks.Count - 1]._targetObject._type == ObjectType.Submarine && _alignmentTasks[_alignmentTasks.Count - 1]._targetObject.isSubmerged() && base.StateMachine.CurrentState.Priority > engageSurfaceContact.Priority;
		AtAny(engageSubSurfaceContact, condition8);
		At(engageSubSurfaceContact, state_default, () => engageSubSurfaceContact.IsFinished);
		EngageAirContact engageAirContact = new EngageAirContact(this, 3);
		Func<bool> condition9 = () => AllowAI() && base.Aligning && _alignmentTasks[_alignmentTasks.Count - 1]._targetObject != null && _alignmentTasks[_alignmentTasks.Count - 1]._targetObject.isAirTarget() && base.StateMachine.CurrentState.Priority > engageAirContact.Priority;
		AtAny(engageAirContact, condition9);
		At(engageAirContact, state_default, () => engageAirContact.IsFinished);
		_state_missileEvasion = new MissileEvasion(this, 2);
		BingoFuel bingoFuel = new BingoFuel(this, 1);
		AtAny(bingoFuel, () => base.StateMachine.CurrentState.Priority > bingoFuel.Priority && base.IsControllable && _isInFlight && _homeBase != null && base.RangeOnMap.Value < 0.1f && !TryStartEmergencyRefuel());
		Winchester winchester = new Winchester(this, 2);
		At(winchester, state_default, () => base.CurrentOrder.Value.OrderType != Order.Type.ReturnToBase);
		AtAny(winchester, () => !base.Aligning && CheckWinchester() && base.StateMachine.CurrentState.Priority > winchester.Priority);
		AircraftStates.ReturnToBase rtb = new AircraftStates.ReturnToBase(this, 3);
		At(rtb, state_default, () => base.CurrentOrder.Value.OrderType != Order.Type.ReturnToBase);
		AtAny(rtb, () => base.CurrentOrder.Value.OrderType == Order.Type.ReturnToBase && base.StateMachine.CurrentState.Priority > rtb.Priority);
		PlayerOverride playerOverride = new PlayerOverride(this, 1);
		AtAny(playerOverride, () => _playerCommandOverride && base.StateMachine.CurrentState.Priority > playerOverride.Priority);
		At(playerOverride, state_default, () => playerOverride.IsFinished);
		_state_defaultEvasion = new EvadeMissile(this, 2);
		AtAny(_state_defaultEvasion, () => (!base.IsPlayerObject || Singleton<OptionsManager>.Instance.AircraftMissileEvasion) && !Globals.useAdvancedEvasion && _missileToEvade != null && CanEvadeMissiles() && (base.StateMachine.CurrentState.Priority > _state_defaultEvasion.Priority || base.StateMachine.CurrentState == bingoFuel));
		At(_state_defaultEvasion, state_default, () => _state_defaultEvasion.IsFinished);
		_state_advancedMissileEvasion = new AdvancedMissileEvasion(this, 1);
		AtAny(_state_advancedMissileEvasion, () => (!base.IsPlayerObject || Singleton<OptionsManager>.Instance.AircraftMissileEvasion) && Globals.useAdvancedEvasion && ShouldEvadeMissile() && CanEvadeMissiles() && (base.StateMachine.CurrentState.Priority > _state_advancedMissileEvasion.Priority || base.StateMachine.CurrentState == bingoFuel));
		At(_state_advancedMissileEvasion, state_default, () => _state_advancedMissileEvasion.IsFinished);
		base.StateMachine.setState(state_noMotionController);
	}

	private void At(IState from, IState to, Func<bool> condition)
	{
		base.StateMachine.addTransition(from, to, condition);
	}

	private void AtAny(IState to, Func<bool> condition)
	{
		base.StateMachine.addAnyTransition(to, condition);
	}

	public bool AllowAI()
	{
		return DM_TestTools._aircraftAIWorksForPlayer;
	}

	private bool TryStartEmergencyRefuel()
	{
		if (!base.IsPlayerObject || base._taskforce == null)
		{
			return false;
		}
		Order value = base.CurrentOrder.Value;
		if (value != null && value.OrderType == Order.Type.Refuel && base.CurrentOrder.Value.TargetUnit is Aircraft tanker && _userRoot?.GetRefuelTask(tanker) != null)
		{
			return true;
		}
		Aircraft aircraft = null;
		float num = float.MaxValue;
		foreach (ObjectBase item in base._taskforce._taskforceAircraft)
		{
			Aircraft aircraft2 = item as Aircraft;
			if (CanReachEmergencyRefuelTanker(aircraft2))
			{
				float sqrMagnitude = (aircraft2.transform.position - base.transform.position).sqrMagnitude;
				if (!(sqrMagnitude >= num))
				{
					num = sqrMagnitude;
					aircraft = aircraft2;
				}
			}
		}
		if (aircraft == null)
		{
			return false;
		}
		setOrder(Order.Type.Refuel, aircraft, displayOrderText: true);
		return _userRoot?.GetRefuelTask(aircraft) != null;
	}

	private bool CanReachEmergencyRefuelTanker(Aircraft tanker)
	{
		if (tanker == null || tanker == this || tanker.IsDestroyed || !tanker._isInFlight || tanker._obp == null)
		{
			return false;
		}
		if (tanker._taskforce != base._taskforce)
		{
			return false;
		}
		if (!AerialRefuelingEquipment.TryGetCompatibleSystem(tanker, this, out var _))
		{
			return false;
		}
		bool flag = false;
		foreach (SupplySystem supplySystem in tanker._obp._supplySystems)
		{
			if (supplySystem.IsAerialRefuelSystem && supplySystem.CanRefuel && tanker.SupplyAllowed.Value && supplySystem.IsOn.Value)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		int num = Mathf.Min(2, Ap._speedValuesInMach.Length - 1);
		float num2 = Ap._rangeMultiplierCurve.Evaluate(Ap._speedValuesInMach[num]);
		num2 /= Ap._altitudeEfficiencyCurve.Evaluate(getHeightInFeet());
		num2 *= 1f + Ap._loadoutMass / Ap._emptyMass;
		float num3 = base.RangeInKm.Value / Mathf.Max(1f, num2) * 0.9f;
		return (tanker.transform.position - base.transform.position).magnitude * 67.200066f / 1000f <= num3;
	}

	public bool EngageTaskBeyondShotRange(EngageTask task)
	{
		if (task?._targetObject == null || task._ammunition?._ap == null)
		{
			return false;
		}
		if (task._ammunition._ap._type != Ammunition.Type.Missile)
		{
			return false;
		}
		if (!task._targetObject.isAirTarget())
		{
			return false;
		}
		return (task._targetObject.transform.position - base.transform.position).magnitude > GetMaxRangeForAmmo(task._ammunition, task._targetObject) * 1.05f;
	}

	public bool HasMoveToEngageTask()
	{
		foreach (EngageTask currentEngageTask in _currentEngageTasks)
		{
			if (EngageTaskBeyondShotRange(currentEngageTask))
			{
				return true;
			}
		}
		return false;
	}

	public EngageTask GetMoveToEngageTask()
	{
		foreach (EngageTask currentEngageTask in _currentEngageTasks)
		{
			if (EngageTaskBeyondShotRange(currentEngageTask))
			{
				return currentEngageTask;
			}
		}
		return null;
	}

	public bool InAirFormation()
	{
		if (!base.HasControl)
		{
			return false;
		}
		if (HasTemporaryFormationReference)
		{
			if (TemporaryFormationLeader is Aircraft)
			{
				return TemporaryFormationLeader != this;
			}
			return false;
		}
		if (base.Formation == null)
		{
			return false;
		}
		if (!(base.Formation.LeaderStation.UnitObject is Aircraft))
		{
			return false;
		}
		Station stationForUnit = base.Formation.GetStationForUnit(this);
		if (stationForUnit == null)
		{
			return false;
		}
		if (stationForUnit == base.Formation.LeaderStation)
		{
			return false;
		}
		if (stationForUnit.UnitActsIndependently.Value)
		{
			return false;
		}
		return true;
	}

	public void SetTemporaryFormationReference(ObjectBase leader, Vector3 stationPosition)
	{
		TemporaryFormationLeader = leader;
		TemporaryFormationStationPosition = stationPosition;
		_currentWaypointParent = leader;
		base.CommandPosition = ActiveFormationStationGeoPosition;
	}

	public void ClearTemporaryFormationReference()
	{
		if (_currentWaypointParent == TemporaryFormationLeader)
		{
			_currentWaypointParent = null;
		}
		TemporaryFormationLeader = null;
		TemporaryFormationStationPosition = Vector3.zero;
	}

	public override void OnStart()
	{
		base.OnStart();
	}

	public void init(AircraftParameters aircraftParameters)
	{
		Ap = aircraftParameters;
		_rootObject = Ap._rootGameObject;
		base.RigidBody = Ap._rootGameObject.GetComponent<Rigidbody>();
		_groundedPitchAngle = Ap._groundedPitchAngle;
		AircraftAnimation = new AircraftAnimation(this, GetComponentsInChildren<Animation>());
		setName(Ap._objectName);
		AircraftAnimation.initParameters();
		setCameraDistancesToTarget(Ap._defaultCameraDistance, Ap._minCameraDistanceForeAft, Ap._minCameraDistanceBroadside, Ap._cameraPivotHeight, Ap._cameraYOffsetForDirectControl);
		base.RangeInKm.Value = Ap._maxRangeInKm;
		ActualRangeInKm.Value = Ap._maxRangeInKm;
		float num = Ap._maxRangeInKm / 2f * 1000f * 0.0148809375f;
		float num2 = 1f + Ap._loadoutMass / Ap._emptyMass;
		base.RangeOnMap.Value = num / num2;
		_soundHandler = new ObjectSoundHandler(this);
		initStates();
	}

	public override void OnUpdateEveryFrame()
	{
		if (_correctAltitudeOnSpawn)
		{
			CorrectAltitudeOnSpawn();
		}
		if (!_canUpdate || GameTime.IsPaused())
		{
			return;
		}
		base.OnUpdateEveryFrame();
		base.StateMachine.OnUpdate();
		float num = base.transform.position.y * 67.200066f;
		HeightOverTerrain.Value = base.transform.position.y - base.SurfaceHeightInUnity;
		AircraftAnimation.OnUpdate();
		if (base.HasControl && !_collidersEnabled && GameTime.missionElapsedTime - _hasControlStartTime > 5.0)
		{
			EnableColliders();
		}
		if (base.transform.parent == null || base.IsDestroyed)
		{
			CheckAircraftOrHelicopterWaterHit();
			CheckAircraftOrHelicopterTerrainHit();
		}
		else if (base.transform.position.y < 0.0001f)
		{
			base.transform.position = new Vector3(base.transform.position.x, 0.01f, base.transform.position.z);
			if (Utils.WrapAngle(base.transform.eulerAngles.x) > 0f)
			{
				base.transform.eulerAngles = new Vector3(3f, base.transform.eulerAngles.y, base.transform.eulerAngles.z);
			}
		}
		if (Motioncontroller != null)
		{
			Motioncontroller.OnUpdate();
			setVelocityInKnots(Motioncontroller.Velocity * 1.94384f);
			_velocityInUnity = _velocityInKnots * 0.0076554087f;
			_velocityVecInUnity = _velocityInUnity * base.transform.forward;
			_soundHandler.OnUpdate();
			UpdateEngineSound(Motioncontroller.ThrustPercent);
			Ap._objectResourcesRoot.transform.position = base.transform.position;
			Ap._objectResourcesRoot.transform.rotation = base.transform.rotation;
			Ap._currentFuelMass = Ap._maxFuel * base.RangeInKm.Value / Ap._maxRangeInKm;
			EnduranceInSec.Value = 1000f * base.RangeInKm.Value / Motioncontroller.Velocity;
			FlightTimeLeftInSec.Value = 1000f * ActualRangeInKm.Value / Motioncontroller.Velocity;
			if (Ap._tipVortexLeft != null)
			{
				if (Ap._numberOfWingSweepAngles > 1)
				{
					Ap._tipVortexLeft.transform.parent.localRotation = Quaternion.Euler(0f, Ap._leftWing.transform.localEulerAngles.y, 0f);
				}
				if (Motioncontroller.GLoad > 2f && Motioncontroller.Velocity > 25f && !Ap._tipVortexLeft.isPlaying)
				{
					Ap._tipVortexLeft.Play();
				}
				else if ((Motioncontroller.GLoad <= 2f || Motioncontroller.Velocity <= 25f) && Ap._tipVortexLeft.isPlaying)
				{
					Ap._tipVortexLeft.Pause();
				}
			}
			if (Ap._tipVortexRight != null)
			{
				if (Ap._numberOfWingSweepAngles > 1)
				{
					Ap._tipVortexRight.transform.parent.localRotation = Quaternion.Euler(0f, Ap._rightWing.transform.localEulerAngles.y, 0f);
				}
				if (Motioncontroller.GLoad > 2f && Motioncontroller.Velocity > 25f && !Ap._tipVortexRight.isPlaying)
				{
					Ap._tipVortexRight.Play();
				}
				else if ((Motioncontroller.GLoad <= 2f || Motioncontroller.Velocity <= 25f) && Ap._tipVortexRight.isPlaying)
				{
					Ap._tipVortexRight.Stop();
				}
			}
			if (Ap._wingVortexRight != null)
			{
				if (Ap._numberOfWingSweepAngles > 1)
				{
					Ap._wingVortexRight.transform.parent.localRotation = Quaternion.Euler(0f, Ap._rightWing.transform.localEulerAngles.y, 0f);
				}
				if (Motioncontroller.PitchAngle > 10f && Motioncontroller.Velocity > 25f && num < 500f && !Ap._wingVortexRight.isPlaying)
				{
					Ap._wingVortexRight.Play();
				}
				else if ((Motioncontroller.PitchAngle <= 10f || Motioncontroller.Velocity <= 25f || num >= 500f) && Ap._wingVortexRight.isPlaying)
				{
					Ap._wingVortexRight.Stop();
				}
			}
			if (Ap._wingVortexLeft != null)
			{
				if (Ap._numberOfWingSweepAngles > 1)
				{
					Ap._wingVortexLeft.transform.parent.localRotation = Quaternion.Euler(0f, Ap._leftWing.transform.localEulerAngles.y, 0f);
				}
				if (Motioncontroller.PitchAngle > 10f && Motioncontroller.Velocity > 25f && num < 500f && !Ap._wingVortexLeft.isPlaying)
				{
					Ap._wingVortexLeft.Play();
				}
				else if ((Motioncontroller.PitchAngle <= 10f || Motioncontroller.Velocity <= 25f || num >= 500f) && Ap._wingVortexLeft.isPlaying)
				{
					Ap._wingVortexLeft.Stop();
				}
			}
			if (Ap._wingVortex != null)
			{
				if (Motioncontroller.PitchAngle > 10f && Motioncontroller.Velocity > 25f && num < 500f && !Ap._wingVortex.isPlaying)
				{
					Ap._wingVortex.Play();
				}
				else if ((Motioncontroller.PitchAngle <= 10f || Motioncontroller.Velocity <= 25f || num >= 500f) && Ap._wingVortex.isPlaying)
				{
					Ap._wingVortex.Stop();
				}
			}
			if (Ap._shockCone != null)
			{
				if (!Ap._shockCone.isPlaying && (double)MachNumber >= 0.99 && (double)MachNumber <= 1.01)
				{
					Ap._shockCone.Play();
				}
				else if (Ap._shockCone.isPlaying && ((double)MachNumber < 0.99 || (double)MachNumber > 1.01))
				{
					Ap._shockCone.Stop();
				}
			}
		}
		StartEnginesOnFlightDeck();
		StopEnginesOnFlightDeck();
	}

	public override void OnFixedUpdate()
	{
		if (!_canUpdate)
		{
			return;
		}
		if (base.Disabled)
		{
			base.OnFixedUpdate();
			return;
		}
		if (base.HasControl && Motioncontroller == null)
		{
			SetFlightPhysicsModel(isStartingAirborne: true);
		}
		if (!base.IsDestroyed)
		{
			if (_externalDestructionNotified)
			{
				SetAircraftOrHelicopterDestroyed();
				SetCrashPysicsModel();
			}
			else if (Singleton<SceneCreator>.Instance._airUnitsRebaseFinished && _homeBase == null && Ap._currentFuelMass < 0f && base._taskforce != Globals._neutralTaskforce)
			{
				setDestroyedFlag();
				base.HasControl = false;
				SetNoFuelFallPysicsModel();
			}
		}
		if (_destroyedInSave && !(Motioncontroller is FixedWingCrashPhysics))
		{
			base.HasControl = false;
			SetCrashPysicsModel();
		}
		if (base.HasControl && !_collidersEnabled && GameTime.missionElapsedTime - _hasControlStartTime > 5.0)
		{
			EnableColliders();
			_collidersEnabled = true;
		}
		if (Motioncontroller != null)
		{
			if (base.HasControl)
			{
				Motioncontroller.CommandVelocity = base.SpeedCommand.Value.GetDemandedSpeedInMetresPerSecond(this);
			}
			Motioncontroller.OnFixedUpdate();
		}
		base.OnFixedUpdate();
		if (base.StateMachine != null)
		{
			base.StateMachine.OnFixedUpdate();
		}
		CheckForThreateningMissile();
	}

	public override void notifyOfHit(RaycastHit hit, AmmunitionParameters.Penetration penetration, AmmunitionParameters.ImpactSize impactSize, bool hasFuel, float remainingRangePercent)
	{
		if (!DM._canKill || (DM._godMode && base.IsPlayerUnit) || base.IsDestroyed || _externalDestructionNotified)
		{
			return;
		}
		foreach (BaseSystem system in _obp._systems)
		{
			system.MakeInoperable();
		}
		notifyOfExternalDestruction();
	}

	public bool CheckWinchester()
	{
		if (!base.IsControllable)
		{
			return false;
		}
		if (!_isInFlight)
		{
			return false;
		}
		if (_homeBase == null)
		{
			return false;
		}
		if (_homeBase._taskforce != base._taskforce)
		{
			_homeBase = null;
			_searchForHomeBase = true;
			return false;
		}
		if (!_checkForWinchester)
		{
			return false;
		}
		if (!base.TacticalDoctrine.PlanesWinchester.ResolvedValue)
		{
			return false;
		}
		if (_currentLoadoutCapabilities._hasSRAAM && HasHostileAirContactWithin(275.59497f))
		{
			return false;
		}
		if (base.FollowingFormation)
		{
			Station stationForUnit = base.Formation.GetStationForUnit(this);
			if (stationForUnit.Role == StationRole.CAP && _currentLoadoutCapabilities.HasAAM && (_ai._objectToDestroy != null || _ai._objectsToDestroyList.Count > 0))
			{
				return false;
			}
			if (stationForUnit.Role == StationRole.Intercept && _currentLoadoutCapabilities.HasAAM)
			{
				return false;
			}
		}
		if (_initialLoadoutCapabilities.CanStrike)
		{
			if (_currentLoadoutCapabilities.CanStrike)
			{
				return false;
			}
			if (_initialLoadoutCapabilities.HasMLRAAM && _currentLoadoutCapabilities.HasMLRAAM)
			{
				return false;
			}
			if (base.IsPlayerObject)
			{
				if (Ap._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Fighter))
				{
					if (base.TacticalDoctrine.FighterRtbCondition.ResolvedValue == TacticalDoctrine.RtbCondition.AllAamAndGunExpended && (_currentLoadoutCapabilities.HasAAM || _currentLoadoutCapabilities._hasGun))
					{
						return false;
					}
					if (base.TacticalDoctrine.FighterBombersStaysWithAAM.ResolvedValue && _currentLoadoutCapabilities.HasAAM)
					{
						return false;
					}
					if (base.TacticalDoctrine.SRFightersStaysWithGuns.ResolvedValue && !_initialLoadoutCapabilities.HasMLRAAM && _currentLoadoutCapabilities._hasGun)
					{
						return false;
					}
				}
				else if (base.TacticalDoctrine.BombersStaysWithAAM.ResolvedValue && _currentLoadoutCapabilities.HasAAM)
				{
					return false;
				}
			}
			return true;
		}
		if (_initialLoadoutCapabilities._hasASWTorpedoes)
		{
			if (_currentLoadoutCapabilities._hasASWTorpedoes)
			{
				return false;
			}
			if (_initialLoadoutCapabilities.HasMLRAAM && _currentLoadoutCapabilities.CanStrike)
			{
				return false;
			}
			if (base.IsPlayerObject)
			{
				if (base.TacticalDoctrine.ASWBombersStaysWithSonobuoys.ResolvedValue && _currentLoadoutCapabilities._hasSonobuoys)
				{
					return false;
				}
				if (Ap._unitRoles.Contains(ObjectBaseParameters.UnitRoles.Fighter))
				{
					if (base.TacticalDoctrine.FighterRtbCondition.ResolvedValue == TacticalDoctrine.RtbCondition.AllAamAndGunExpended && (_currentLoadoutCapabilities.HasAAM || _currentLoadoutCapabilities._hasGun))
					{
						return false;
					}
					if (base.TacticalDoctrine.FighterBombersStaysWithAAM.ResolvedValue && _currentLoadoutCapabilities.HasAAM)
					{
						return false;
					}
					if (base.TacticalDoctrine.SRFightersStaysWithGuns.ResolvedValue && !_initialLoadoutCapabilities.HasMLRAAM && _currentLoadoutCapabilities._hasGun)
					{
						return false;
					}
				}
				else if (base.TacticalDoctrine.BombersStaysWithAAM.ResolvedValue && _currentLoadoutCapabilities.HasAAM)
				{
					return false;
				}
			}
			return true;
		}
		if (_initialLoadoutCapabilities.HasMLRAAM)
		{
			if (_currentLoadoutCapabilities.HasMLRAAM)
			{
				return false;
			}
			if (base.IsPlayerObject && base.TacticalDoctrine.FighterRtbCondition.ResolvedValue == TacticalDoctrine.RtbCondition.AllAamAndGunExpended && (_currentLoadoutCapabilities.HasAAM || _currentLoadoutCapabilities._hasGun))
			{
				return false;
			}
			if (base.IsPlayerObject && base.TacticalDoctrine.FighterRtbCondition.ResolvedValue == TacticalDoctrine.RtbCondition.AllAamExpended && _currentLoadoutCapabilities.HasAAM)
			{
				return false;
			}
			return true;
		}
		if (_initialLoadoutCapabilities.HasAAM)
		{
			if (_currentLoadoutCapabilities.HasAAM)
			{
				return false;
			}
			if (base.IsPlayerObject && base.TacticalDoctrine.FighterRtbCondition.ResolvedValue == TacticalDoctrine.RtbCondition.AllAamAndGunExpended && _currentLoadoutCapabilities._hasGun)
			{
				return false;
			}
			if (base.IsPlayerObject && base.TacticalDoctrine.SRFightersStaysWithGuns.ResolvedValue && _currentLoadoutCapabilities._hasGun)
			{
				return false;
			}
			return true;
		}
		if (_initialLoadoutCapabilities._hasGun && !_currentLoadoutCapabilities._hasGun)
		{
			return true;
		}
		if (_initialLoadoutCapabilities._hasSonobuoys && !_currentLoadoutCapabilities._hasSonobuoys)
		{
			return true;
		}
		return false;
	}

	private bool HasHostileAirContactWithin(float rangeUnity)
	{
		if (base._taskforce?.PlottingTable == null)
		{
			return false;
		}
		float num = rangeUnity * rangeUnity;
		foreach (KeyValuePair<ObjectBase, Vehicle> localVehicle in base._taskforce.PlottingTable.LocalVehicles)
		{
			ObjectBase key = localVehicle.Key;
			Vehicle value = localVehicle.Value;
			if (!(key == null) && !key.IsDestroyed && key.IsAirUnit && value != null && !value.IsDormant.Value && value.CurrentRelationship() == RelationsState.Hostile && (key.transform.position - base.transform.position).sqrMagnitude < num)
			{
				return true;
			}
		}
		return false;
	}

	public void setRudderBasedOnCourse()
	{
		if (base.CourseCommand.Value == null)
		{
			GeoPosition? commandPosition = base.CommandPosition;
			if (commandPosition.HasValue)
			{
				GeoPosition valueOrDefault = commandPosition.GetValueOrDefault();
				base.IsLoitering = false;
				float item = base.Position.Value.HeadingRangeMeters(valueOrDefault).range;
				item *= 0.0005399568f;
				if (!_isCurrentWaypointReachable && _currentWaypointParent != null)
				{
					Order value = base.CurrentOrder.Value;
					if (value == null || value.OrderType != Order.Type.Refuel)
					{
						float num = 0.2f;
						float num2 = 1.5f;
						_isSpeedCommandOverridesInfo = true;
						if (item < num && _velocityInKnots > _currentWaypointParent._velocityInKnots)
						{
							if (_currentWaypointParent._velocityInKnots > MaxForwardSpeedInKnots)
							{
								SetSpeedCommand(new ConstantSpeed(MaxForwardSpeedInKnots, this));
							}
							else
							{
								SetSpeedCommand(new ConstantSpeed(_currentWaypointParent._velocityInKnots, this));
							}
						}
						else if (item < num2)
						{
							float num3 = (item - num) / (num2 - num);
							float num4 = 25f + _currentWaypointParent._velocityInKnots + 40f * num3;
							if (num4 > MaxForwardSpeedInKnots)
							{
								SetSpeedCommand(new ConstantSpeed(MaxForwardSpeedInKnots, this));
							}
							else
							{
								SetSpeedCommand(new ConstantSpeed(num4, this));
							}
						}
						else
						{
							SetSpeedCommand(new ConstantSpeed(MaxForwardSpeedInKnots, this));
						}
					}
				}
			}
		}
		if (base.Formation?.LeaderStation?.UnitObject == this && _velocityInKnots > base.Formation.MaxFormationSpeed.Value)
		{
			_isSpeedCommandOverridesInfo = true;
			SetSpeedCommand(new ConstantSpeed(base.Formation.MaxFormationSpeed.Value - 1f, this));
		}
	}

	public MotionController SetFlightPhysicsModel(bool isStartingAirborne = false)
	{
		MotionController motionController = Ap._rootGameObject.AddComponent<FixedWingFlightPhysics>();
		motionController.init(Motioncontroller);
		if (isStartingAirborne)
		{
			base.DesiredAltitude.Value = base.transform.position.y;
			if (!base.SpeedCommand.HasValue || base.SpeedCommand.Value == null)
			{
				SetSpeedCommand(new TelegraphSpeed(this, 3));
			}
			motionController.CommandVelocity = base.SpeedCommand.Value.GetDemandedSpeedInMetresPerSecond(this);
			if (Motioncontroller == null)
			{
				motionController.SetVelocity(motionController.CommandVelocity);
			}
			base.CommandPosition = base._geoPosition;
		}
		if (Motioncontroller != null)
		{
			UnityEngine.Object.Destroy(Motioncontroller);
		}
		Motioncontroller = motionController;
		if (isStartingAirborne)
		{
			Motioncontroller.OnFixedUpdate();
		}
		return Motioncontroller;
	}

	public MotionController SetFormationPhysicsModel()
	{
		MotionController motionController = Ap._rootGameObject.AddComponent<FormationFlightPhysics>();
		motionController.init(Motioncontroller);
		if (Motioncontroller != null)
		{
			UnityEngine.Object.Destroy(Motioncontroller);
		}
		Motioncontroller = motionController;
		Motioncontroller.OnFixedUpdate();
		return Motioncontroller;
	}

	public MotionController SetCrashPysicsModel()
	{
		MotionController motionController = Ap._rootGameObject.AddComponent<FixedWingCrashPhysics>();
		motionController.init(Motioncontroller);
		if (Motioncontroller != null)
		{
			UnityEngine.Object.Destroy(Motioncontroller);
		}
		Motioncontroller = motionController;
		return Motioncontroller;
	}

	public MotionController SetNoFuelFallPysicsModel()
	{
		MotionController motionController = Ap._rootGameObject.AddComponent<FixedWingNoFuelFallPhysics>();
		motionController.init(Motioncontroller);
		if (Motioncontroller != null)
		{
			UnityEngine.Object.Destroy(Motioncontroller);
		}
		Motioncontroller = motionController;
		return Motioncontroller;
	}

	public MotionController SetVtolPysicsModel(bool makeStationary = false)
	{
		MotionController motionController = Ap._rootGameObject.AddComponent<VTOLFlightPhysics>();
		motionController.init(Motioncontroller);
		if (makeStationary)
		{
			motionController.SetBankRate(0f);
			motionController.SetYawRate(0f);
			motionController.SetPitchRate(0f);
			motionController.SetVelocity(0f);
			motionController.SetThrust(Ap.Mass * 9.81f);
		}
		if (Motioncontroller != null)
		{
			UnityEngine.Object.Destroy(Motioncontroller);
		}
		Motioncontroller = motionController;
		return Motioncontroller;
	}

	public MotionController SetTaxiPhysicsModel(FlightDeck flightDeck, bool isBackwards, bool isSlow, float commandHeading = float.NaN, float commandVelocity = float.NaN, float accelerationLimit = float.NaN)
	{
		GroundTaxiPhysics groundTaxiPhysics = Ap._rootGameObject.AddComponent<GroundTaxiPhysics>();
		groundTaxiPhysics._flightDeck = flightDeck;
		if (isSlow)
		{
			groundTaxiPhysics.SlowTaxi();
		}
		if (isBackwards)
		{
			groundTaxiPhysics.BackTaxi();
		}
		groundTaxiPhysics.init(Motioncontroller);
		if (!float.IsNaN(commandHeading))
		{
			groundTaxiPhysics.CommandHeading = commandHeading;
		}
		if (!float.IsNaN(commandVelocity))
		{
			groundTaxiPhysics.CommandVelocity = commandVelocity;
		}
		if (!float.IsNaN(accelerationLimit))
		{
			groundTaxiPhysics.linearAccelerationLimit = accelerationLimit;
		}
		if (Motioncontroller != null)
		{
			UnityEngine.Object.Destroy(Motioncontroller);
		}
		Motioncontroller = groundTaxiPhysics;
		return Motioncontroller;
	}

	public MotionController SetTakeoffPhysicsModel(float commandHeading, bool useCatapult = false, float catapultLength = 0f, bool useSkijump = false, AnimationCurve skijumpOffsetCurve = null, bool useVtol = false, bool useStol = false)
	{
		MotionController motionController;
		if (useVtol)
		{
			motionController = Ap._rootGameObject.AddComponent<VTOLFlightPhysics>();
		}
		else if (useCatapult)
		{
			motionController = Ap._rootGameObject.AddComponent<CatapultTakeoffPhysics>();
			((CatapultTakeoffPhysics)motionController).catapultLength = catapultLength;
		}
		else
		{
			motionController = Ap._rootGameObject.AddComponent<FixedWingTakeoffPhysics>();
			((FixedWingTakeoffPhysics)motionController).useSkijump = useSkijump;
			((FixedWingTakeoffPhysics)motionController).skijumpOffsetCurve = skijumpOffsetCurve;
		}
		motionController.init(Motioncontroller);
		motionController.CommandHeading = commandHeading;
		if (Motioncontroller != null)
		{
			UnityEngine.Object.Destroy(Motioncontroller);
		}
		Motioncontroller = motionController;
		return Motioncontroller;
	}

	public int setPresetHeight(int preset, bool updateAltForWaypoints = false)
	{
		if (preset > Ap._presetAltitudes.Length - 1)
		{
			preset = Ap._presetAltitudes.Length - 1;
		}
		base.DesiredAltitude.Value = Ap._presetAltitudes[preset];
		if (updateAltForWaypoints)
		{
			UpdateWaypointsAltitudes();
		}
		UpdateAlignmentAltitudes();
		return (int)(Ap._presetAltitudes[preset] * 220.47266f);
	}

	private void UpdateEngineSound(float mainThrustPercent)
	{
		if (!engineOn)
		{
			return;
		}
		float num = mainThrustPercent;
		if (num > 1f)
		{
			num = 1f;
		}
		if (Ap._engineAudioSource != null && !_externalDestructionNotified)
		{
			float x = Ap._engineAudioPitchRange.x;
			float num2 = Ap._engineAudioPitchRange.y - x;
			float pitch = x + num * num2;
			Ap._engineAudioSource.pitch = pitch;
		}
		if (Ap._exhaustAudioSource != null && !_externalDestructionNotified)
		{
			float x2 = Ap._exhaustAudioPitchRange.x;
			float num3 = Ap._exhaustAudioPitchRange.y - x2;
			float pitch2 = x2 + num * num3;
			Ap._exhaustAudioSource.pitch = pitch2;
		}
		if (Ap._farAudioSource != null && !_externalDestructionNotified)
		{
			Ap._farAudioSource.pitch = Ap._farAudioPitch;
		}
		if (Ap._engineType == EngineType.Jet && Ap._afterburnerAudioSource != null && !_externalDestructionNotified)
		{
			if (mainThrustPercent > 1f && !Ap._afterburnerAudioSource.isPlaying)
			{
				Ap._afterburnerAudioSource.Play();
			}
			else if (mainThrustPercent <= 1f && Ap._afterburnerAudioSource.isPlaying)
			{
				Ap._afterburnerAudioSource.Stop();
			}
		}
	}

	public override void enableSoundWhenSwitchingUnit()
	{
		if (engineOn)
		{
			StartAllEngineAudio();
		}
	}

	public override void disableSoundInPauseModeWhenSwitchingUnit()
	{
		if (GameTime.IsPaused())
		{
			if (Ap._engineAudioSource != null && Ap._engineAudioSource.isPlaying)
			{
				Ap._engineAudioSource.Pause();
			}
			if (Ap._exhaustAudioSource != null && Ap._exhaustAudioSource.isPlaying)
			{
				Ap._exhaustAudioSource.Pause();
			}
			if (Ap._farAudioSource != null && Ap._farAudioSource.isPlaying)
			{
				Ap._farAudioSource.Pause();
			}
		}
	}

	public override void setTelegraph(int telegraph)
	{
		if (telegraph > Ap._speedValuesInMach.Length)
		{
			telegraph = Ap._speedValuesInMach.Length;
		}
		_telegraph = ((telegraph > 0) ? (telegraph - 1) : 0);
		SetSpeedCommand(new ConstantMach(Ap._speedValuesInMach[_telegraph], this));
	}

	public override int getTelegraph()
	{
		return _telegraph;
	}

	public void setHomeBase(ObjectBase homeBase, GameObject parent)
	{
		_homeBase = homeBase;
		Ap._aircraftHomeObject = parent;
		if (parent != null)
		{
			Ap._rootGameObject.transform.SetParent(Ap._aircraftHomeObject.transform);
		}
		_velocityInKnots = 0f;
		setTelegraph(1);
	}

	public bool preflightChecks(float startTime)
	{
		return true;
	}

	public void testLaunchWeapon(int containerIdx)
	{
		WeaponSystem weaponSystem = Ap._weaponSystems[Globals._testWeaponSystem - 1];
		if (weaponSystem.GetType() == typeof(WeaponSystemLauncher))
		{
			((WeaponSystemLauncher)weaponSystem).testLaunchWeapon(containerIdx);
		}
	}

	public void giveControl(float velocity, bool resetAltitude = true)
	{
		base.HasControl = true;
		_hasControlStartTime = GameTime.missionElapsedTime;
		if (Ap._rootGameObject != null)
		{
			Ap._rootGameObject.transform.SetParent(null);
		}
		if (resetAltitude)
		{
			base.DesiredAltitude.Value = base.transform.position.y;
		}
		_velocityInKnots = velocity;
		_velocityInUnity = _velocityInKnots * 0.0076554087f;
	}

	public void exportToIniFile()
	{
	}

	public void ImmediateStartEngines()
	{
		foreach (GameObject item in Ap._propsIdle)
		{
			item.SetActive(value: false);
		}
		foreach (GameObject item2 in Ap._propsInFlight)
		{
			item2.SetActive(value: true);
		}
		engineOn = true;
		StartAllEngineAudio();
		if (Ap._exhaustObject != null)
		{
			Ap._exhaustObject.SetActive(value: true);
		}
	}

	public void ImmediateStopEngines()
	{
		foreach (GameObject item in Ap._propsIdle)
		{
			item.SetActive(value: true);
		}
		foreach (GameObject item2 in Ap._propsInFlight)
		{
			item2.SetActive(value: false);
		}
		engineOn = false;
		StopAllEngineAudio();
		if (Ap._exhaustObject != null)
		{
			Ap._exhaustObject.SetActive(value: false);
		}
	}

	private void StartEnginesOnFlightDeck()
	{
		if (!EngineStarted || engineOn)
		{
			return;
		}
		StartAllEngineAudio();
		if (GameTime.missionElapsedTime - _startTime < 30.0)
		{
			float num = (float)(GameTime.missionElapsedTime - _startTime) / 30f;
			if (!propsSwitched)
			{
				float num2 = num * GameTime.deltaTime * 2500f;
				for (int i = 0; i < Ap._propsIdle.Count; i++)
				{
					if (!(Ap._propsIdle[i].transform == null))
					{
						Ap._propsIdle[i].transform.Rotate((Ap._propsIdleForwardAxes[i] != AircraftFlightControlSystem.RotationAxis.X) ? 0f : (Ap._propClockwise[i] ? num2 : (0f - num2)), (Ap._propsIdleForwardAxes[i] != AircraftFlightControlSystem.RotationAxis.Y) ? 0f : (Ap._propClockwise[i] ? num2 : (0f - num2)), (Ap._propsIdleForwardAxes[i] != AircraftFlightControlSystem.RotationAxis.Z) ? 0f : (Ap._propClockwise[i] ? num2 : (0f - num2)));
					}
				}
				if ((double)num > 0.5)
				{
					foreach (GameObject item in Ap._propsIdle)
					{
						item.SetActive(value: false);
					}
					foreach (GameObject item2 in Ap._propsInFlight)
					{
						item2.SetActive(value: true);
					}
					if ((bool)Ap._farAudioSource)
					{
						Ap._farAudioSource.volume = 0f;
					}
					propsSwitched = true;
				}
			}
			if (Ap._engineAudioSource != null)
			{
				Ap._engineAudioSource.pitch = Ap._engineAudioPitchRange.x * num;
			}
			if (Ap._exhaustAudioSource != null)
			{
				Ap._exhaustAudioSource.pitch = Ap._exhaustAudioPitchRange.x * num;
			}
		}
		else
		{
			engineOn = true;
			EngineStarted = false;
		}
	}

	private void StopEnginesOnFlightDeck()
	{
		if (!EngineStopped || !engineOn)
		{
			return;
		}
		if (GameTime.missionElapsedTime - _startTime < 30.0)
		{
			float num = 1f - (float)(GameTime.missionElapsedTime - _startTime) / 30f;
			if (num < 0.5f && propsSwitched)
			{
				if (Ap._exhaustObject != null)
				{
					Ap._exhaustObject.SetActive(value: false);
				}
				if (Ap._farAudioSource != null)
				{
					Ap._farAudioSource.Stop();
				}
				foreach (GameObject item in Ap._propsIdle)
				{
					item?.SetActive(value: true);
				}
				foreach (GameObject item2 in Ap._propsInFlight)
				{
					item2?.SetActive(value: false);
				}
				propsSwitched = false;
			}
			if (num < 0.25f && !audioStopped)
			{
				StopAllEngineAudio();
			}
			if (!propsSwitched)
			{
				float num2 = num * GameTime.deltaTime * 2500f;
				for (int i = 0; i < Ap._propsIdle.Count; i++)
				{
					if (!(Ap._propsIdle[i]?.transform == null))
					{
						Ap._propsIdle[i].transform.Rotate((Ap._propsIdleForwardAxes[i] != AircraftFlightControlSystem.RotationAxis.X) ? 0f : (Ap._propClockwise[i] ? num2 : (0f - num2)), (Ap._propsIdleForwardAxes[i] != AircraftFlightControlSystem.RotationAxis.Y) ? 0f : (Ap._propClockwise[i] ? num2 : (0f - num2)), (Ap._propsIdleForwardAxes[i] != AircraftFlightControlSystem.RotationAxis.Z) ? 0f : (Ap._propClockwise[i] ? num2 : (0f - num2)));
					}
				}
			}
			if (Ap._engineAudioSource != null)
			{
				Ap._engineAudioSource.pitch = Ap._engineAudioPitchRange.x * num;
			}
			if (Ap._exhaustAudioSource != null)
			{
				Ap._exhaustAudioSource.pitch = Ap._exhaustAudioPitchRange.x * num;
			}
		}
		else
		{
			engineOn = false;
			propsSwitched = false;
			audioStopped = false;
			EngineStopped = false;
		}
	}

	private void StartAllEngineAudio()
	{
		if ((bool)Ap._engineAudioSource && !Ap._engineAudioSource.isPlaying)
		{
			Ap._engineAudioSource.Play();
		}
		if ((bool)Ap._exhaustAudioSource && !Ap._exhaustAudioSource.isPlaying)
		{
			Ap._exhaustAudioSource.Play();
		}
		if ((bool)Ap._farAudioSource && !Ap._farAudioSource.isPlaying)
		{
			Ap._farAudioSource.Play();
			Ap._farAudioSource.volume = 0f;
		}
	}

	private void StopAllEngineAudio()
	{
		if ((bool)Ap._engineAudioSource && Ap._engineAudioSource.isPlaying)
		{
			Ap._engineAudioSource.Stop();
		}
		if ((bool)Ap._exhaustAudioSource && Ap._exhaustAudioSource.isPlaying)
		{
			Ap._exhaustAudioSource.Stop();
		}
		if ((bool)Ap._farAudioSource && Ap._farAudioSource.isPlaying)
		{
			Ap._farAudioSource.Stop();
		}
		if ((bool)Ap._afterburnerAudioSource && Ap._afterburnerAudioSource.isPlaying)
		{
			Ap._afterburnerAudioSource.Stop();
		}
	}

	private void CorrectAltitudeOnSpawn()
	{
		bool flag = _altitudeCorrection == AltitudeCorrection.Cruise || _altitudeCorrection == AltitudeCorrection.Ceiling;
		TerrainChunk value = null;
		if ((!flag && !Singleton<TerrainManager>.Instance.TerrainChunkDictionary.TryGetValue(Utils.longLatToTile(base._geoPosition), out value)) || (!flag && (value == null || !value._isFinalized)))
		{
			return;
		}
		_correctAltitudeOnSpawn = false;
		switch (_altitudeCorrection)
		{
		case AltitudeCorrection.VeryLow:
			setPresetHeight(0);
			break;
		case AltitudeCorrection.Low:
			setPresetHeight(1);
			break;
		case AltitudeCorrection.Medium:
			setPresetHeight(2);
			break;
		case AltitudeCorrection.High:
			setPresetHeight(3);
			break;
		case AltitudeCorrection.VeryHigh:
			setPresetHeight(4);
			break;
		case AltitudeCorrection.Cruise:
			setPresetHeight(Ap._presetAltitudes.Length - 2);
			break;
		case AltitudeCorrection.Ceiling:
			setPresetHeight(Ap._presetAltitudes.Length - 1);
			break;
		}
		if (_altitudeCorrection != AltitudeCorrection.None)
		{
			base.transform.position = new Vector3(base.transform.position.x, base.DesiredAltitude.Value, base.transform.position.z);
			SetTeleportProtection();
		}
		float num = TerrainUtils.GetHeightAt(base._geoPosition) + 0.2267855f;
		if (num < 0f)
		{
			num = 0f;
		}
		if (base.transform.position.y < num)
		{
			base.transform.position = new Vector3(base.transform.position.x, num, base.transform.position.z);
			SetTeleportProtection();
		}
		SetDesiredAltitudeToCurrentWaypoint();
		foreach (TrailRenderer trailRenderer in Ap._trailRenderers)
		{
			if (!(trailRenderer == null))
			{
				trailRenderer.Clear();
				TrailRendererScaler component = trailRenderer.gameObject.GetComponent<TrailRendererScaler>();
				if (component != null)
				{
					component.resetTimer();
				}
			}
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (_collisionNoticed || other.transform.gameObject.layer == 17 || other.name == "col_missile_proximity" || other.name == "blunt collider")
		{
			return;
		}
		ObjectBase componentInChildren = other.transform.root.GetComponentInChildren<ObjectBase>();
		if (!(componentInChildren == null) && componentInChildren.isUnit() && (componentInChildren._taskforce != base._taskforce || !componentInChildren.IsAirUnit))
		{
			Vector3 position = other.ClosestPoint(base.transform.position);
			base.transform.position = position;
			Effect hitEffect = _obp._hitGroundEffect;
			if (componentInChildren.IsAirUnit)
			{
				hitEffect = _obp._hitAirEffect;
			}
			base.Info.LogUnitInfo("Collided with " + componentInChildren.getUIDAndName());
			PlayExplosionEffect(hitEffect);
			if (componentInChildren is Vessel || componentInChildren is Submarine || componentInChildren is LandUnit)
			{
				destroyObject();
			}
			Blastzone blastzone = new Blastzone(new AmmunitionParameters
			{
				_addFire = 3,
				_impactSize = AmmunitionParameters.ImpactSize.Medium,
				_penetration = AmmunitionParameters.Penetration.Always,
				_warheadPower = 50f
			}, base.transform.position, Vector3.zero, this);
			blastzone._isSecondaryExplosion = true;
			blastzone.CreateExplosion();
			_collisionNoticed = true;
		}
	}

	public void UpdateExhaustDamageTint()
	{
		if (Ap == null || Ap._exhaustObject == null)
		{
			return;
		}
		if (_exhaustBaseColors == null)
		{
			_exhaustBaseColors = new List<(ParticleSystem, ParticleSystem.MinMaxGradient)>();
			ParticleSystem[] componentsInChildren = Ap._exhaustObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
			foreach (ParticleSystem particleSystem in componentsInChildren)
			{
				_exhaustBaseColors.Add((particleSystem, particleSystem.main.startColor));
			}
		}
		float t = (getDamageModelActive() ? 1f : Mathf.Clamp01(GunDamageFraction()));
		float factor = Mathf.Lerp(1f, 0.15f, t);
		foreach (var (particleSystem2, source) in _exhaustBaseColors)
		{
			if (!(particleSystem2 == null))
			{
				ParticleSystem.MainModule main = particleSystem2.main;
				main.startColor = DarkenGradient(source, factor);
			}
		}
	}

	private static ParticleSystem.MinMaxGradient DarkenGradient(ParticleSystem.MinMaxGradient source, float factor)
	{
		return source.mode switch
		{
			ParticleSystemGradientMode.Color => new ParticleSystem.MinMaxGradient(DarkenColor(source.color, factor)), 
			ParticleSystemGradientMode.TwoColors => new ParticleSystem.MinMaxGradient(DarkenColor(source.colorMin, factor), DarkenColor(source.colorMax, factor)), 
			ParticleSystemGradientMode.Gradient => new ParticleSystem.MinMaxGradient(DarkenGradientKeys(source.gradient, factor)), 
			ParticleSystemGradientMode.TwoGradients => new ParticleSystem.MinMaxGradient(DarkenGradientKeys(source.gradientMin, factor), DarkenGradientKeys(source.gradientMax, factor)), 
			_ => source, 
		};
	}

	private static Color DarkenColor(Color color, float factor)
	{
		return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
	}

	private static Gradient DarkenGradientKeys(Gradient source, float factor)
	{
		GradientColorKey[] colorKeys = source.colorKeys;
		for (int i = 0; i < colorKeys.Length; i++)
		{
			colorKeys[i].color = DarkenColor(colorKeys[i].color, factor);
		}
		Gradient gradient = new Gradient();
		gradient.mode = source.mode;
		gradient.SetKeys(colorKeys, source.alphaKeys);
		return gradient;
	}

	protected void PlayExplosionEffect(Effect hitEffect)
	{
		Vector3 position = base.transform.position;
		if (_shotDownFireEffectGO != null)
		{
			if (_shotDownFireEffectGO.transform.parent != null && _shotDownFireEffectGO.transform.parent.tag != "PoolManager")
			{
				_shotDownFireEffectGO.transform.SetParent(null);
				_shotDownFireEffectGO.transform.position = position;
			}
			ParticleUtils.SetAutoDestroyAndCustomWorldSpace(_shotDownFireEffectGO);
			_shotDownFireEffectGO.GetComponent<ParticleAutoDestroy>().setTimerAndStop(15f, 15f);
		}
		GameObject gameObject = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(hitEffect, position, base.transform.rotation, triggerCameraShake: true));
		if (gameObject != null)
		{
			gameObject.GetComponent<ParticleSystem>().Play();
			gameObject.GetComponent<ParticleAutoDestroy>().setTimer(25f);
		}
		else
		{
			Log.Error("Failed to spawn HitGroundEffect for " + getObjectIniName(), Log.Section.General, "E:\\projects\\seapower\\Assets\\Scripts\\Aircraft\\Aircraft.cs", 1711, "PlayExplosionEffect");
		}
		Debug.Log("ObjectBase.CheckAircraftOrHelicopterTerrainHit called");
	}

	public override void RaiseMissileAlert()
	{
		Globals._mainGameViewModel.RaiseAlert(this, Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "MissileIncoming"));
		if (GameTime.missionElapsedTime - Globals._lastIncomingMissileTime < (double)Globals._incomingMissileInterval)
		{
			return;
		}
		Globals._lastIncomingMissileTime = GameTime.missionElapsedTime;
		if (Singleton<OptionsManager>.Instance.ContactVO)
		{
			if (base.ThreateningMissile._launchPlatform != null && !base.ThreateningMissile._launchPlatform.IsAirUnit)
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("MissileIncoming_Sam", this);
			}
			else
			{
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage("MissileIncoming", this);
			}
		}
	}

	public bool CanEvadeMissiles()
	{
		if (_inLevelAttackMode)
		{
			return false;
		}
		if (_inDiveAttackMode)
		{
			return false;
		}
		if (Ap._isLarge)
		{
			return false;
		}
		_ = base.IsFormationLeader;
		return true;
	}

	public override void CeaseFire(bool report = true, bool clearOrder = true, bool formationCheck = true, bool stopJamming = true, bool airFormationCheck = true, bool clearAttackTasks = true)
	{
		if (base.StateMachine.CurrentState == _state_advancedMissileEvasion)
		{
			Missile missileToEvade = _state_advancedMissileEvasion.MissileToEvade;
			if (missileToEvade != null && !missileToEvade.IsDestroyed)
			{
				_ignoredMissiles.Add(missileToEvade);
			}
			_state_advancedMissileEvasion.IsFinished = true;
		}
		else
		{
			base.CeaseFire(report, clearOrder, formationCheck, stopJamming, airFormationCheck, clearAttackTasks);
		}
	}

	private bool ShouldEvadeMissile()
	{
		_ignoredMissiles.RemoveWhere((Missile m) => m == null || m.IsDestroyed);
		Missile threateningMissile = base.ThreateningMissile;
		if (threateningMissile == null || threateningMissile.IsDestroyed || threateningMissile.CurrentTarget != this)
		{
			return false;
		}
		if (_ignoredMissiles.Contains(threateningMissile))
		{
			return false;
		}
		Vector3 vector = base.transform.position - threateningMissile.transform.position;
		float num = Vector3.Dot(threateningMissile._velocityVecInUnity - _velocityVecInUnity, vector.normalized);
		if (num < 0.5f)
		{
			return false;
		}
		float num2 = vector.magnitude / num;
		float num3 = (threateningMissile.IsMotorBurning ? threateningMissile._velocityInKnots : ((threateningMissile._velocityInKnots + threateningMissile._ap.MinVelocity) * 0.5f)) * 0.0076554087f;
		float num4 = MaxForwardSpeedInKnots * 0.514444f;
		float num5 = ((num4 > 0.1f) ? (Ap._maxG * 9.81f / num4 * 57.29578f) : 30f);
		if (num3 - MaxForwardSpeedInKnots * 0.0076554087f <= 2f || num2 > 3f)
		{
			float f = Utils.AngleOffAroundAxis(threateningMissile.transform.position - base.transform.position, base.transform.forward, Vector3.up);
			float num6 = Mathf.Max(0f, 180f - Mathf.Abs(f));
			float num7 = ((num5 > 0.1f) ? (num6 / (num5 * 0.6f)) : 4f);
			return num2 <= num7 + 2f;
		}
		float num8 = ((num5 > 0.1f) ? (90f / (num5 * 0.6f)) : 3f);
		return num2 <= num8 + 5f;
	}

	public void UpdateFuelConsumption(float deltaDistanceInMeters)
	{
		float num = 1f + Ap._loadoutMass / Ap._emptyMass;
		float time = 0f;
		if (Motioncontroller != null)
		{
			time = Motioncontroller.CommandVelocity / Atmosphere.SpeedOfSound(getHeightInMeters());
		}
		_consumptionCoefficient = Ap._rangeMultiplierCurve.Evaluate(time);
		_consumptionCoefficient /= Ap._altitudeEfficiencyCurve.Evaluate(getHeightInFeet());
		_consumptionCoefficient *= num;
		if (!_unlimitedFuel)
		{
			base.RangeInKm.Value -= _consumptionCoefficient * deltaDistanceInMeters / 1000f;
		}
		if (_consumptionCoefficient < 1f)
		{
			_consumptionCoefficient = 1f;
		}
		if (_homeBase == null)
		{
			ActualRangeInKm.Value = base.RangeInKm.Value / _consumptionCoefficient;
			base.RangeOnMap.Value = ActualRangeInKm.Value * 1000f * 0.0148809375f;
		}
		else
		{
			float num2 = (float)base._geoPosition.GetDistance(_homeBase._geoPosition) * 67.200066f / 1000f;
			ActualRangeInKm.Value = (base.RangeInKm.Value - num2 * num) / _consumptionCoefficient;
			float num3 = num2 * num;
			float num4 = (base.RangeInKm.Value - num3) / (num + _consumptionCoefficient);
			base.RangeOnMap.Value = num4 * 1000f * 0.0148809375f;
		}
		if (_unlimitedFuel)
		{
			base.RangeOnMap.Value = 2755922f;
		}
	}

	public float ReceiveFuel(float fuelInKg, float maxFillFraction = 0.9f, bool allowTankerReceiver = false)
	{
		if (1 == 0)
		{
			return 0f;
		}
		float num = Ap._maxFuel * Mathf.Clamp01(maxFillFraction);
		if (Ap._currentFuelMass >= num)
		{
			return 0f;
		}
		if (!allowTankerReceiver)
		{
			foreach (SupplySystem supplySystem in Ap._supplySystems)
			{
				if (supplySystem.IsAerialRefuelSystem)
				{
					return 0f;
				}
			}
		}
		float num2 = Mathf.Min(fuelInKg, num - Ap._currentFuelMass);
		Ap._currentFuelMass += num2;
		base.RangeInKm.Value = Ap._maxRangeInKm * Ap._currentFuelMass / Ap._maxFuel;
		return num2;
	}

	public float GetRangeOnMaxSpeed()
	{
		float num = 1f + Ap._loadoutMass / Ap._emptyMass;
		float time = Ap._speedValuesInMach[Ap._speedValuesInMach.Length - 1];
		if (Ap._speedValuesInMach.Length > 4)
		{
			time = Ap._speedValuesInMach[Ap._speedValuesInMach.Length - 2];
		}
		float num2 = Ap._rangeMultiplierCurve.Evaluate(time);
		num2 /= Ap._altitudeEfficiencyCurve.Evaluate(getHeightInFeet());
		num2 *= num;
		if (num2 < 1f)
		{
			num2 = 1f;
		}
		if (_homeBase == null)
		{
			return base.RangeInKm.Value / num2 * 1000f * 0.0148809375f;
		}
		float num3 = (float)base._geoPosition.GetDistance(_homeBase._geoPosition) * 67.200066f / 1000f * num;
		return (base.RangeInKm.Value - num3) / (num + num2) * 1000f * 0.0148809375f;
	}

	public bool CalculateInterceptPositionForVelocity(float speedValueInMach, ObjectBase targetObject, out Vector3 interceptPosition)
	{
		Vector3 position = targetObject.transform.position;
		if (position.y * 220.47266f < 1000f)
		{
			position.y = 4.53571f;
		}
		else if (position.y > Ap._ceiling)
		{
			position.y = Ap._ceiling;
		}
		float num = Ap._machLimitCurve.Evaluate(position.y * 220.47266f);
		if (speedValueInMach > num)
		{
			speedValueInMach = num;
		}
		float interceptorVelocity = speedValueInMach * Atmosphere.SpeedOfSound(position.y * 67.200066f) * 0.0148809375f;
		(Vector3, bool) tuple = Utils.CalculateInterceptPosition(base.transform.position, interceptorVelocity, targetObject.transform.position, targetObject._velocityVecInUnity);
		(interceptPosition, _) = tuple;
		if (!tuple.Item2)
		{
			return false;
		}
		return true;
	}

	public bool CalculateInterceptPositionForVelocity_alt(float speedValueInMach, ObjectBase targetObject, out Vector3 interceptPosition)
	{
		Vector3 position = targetObject.transform.position;
		if (position.y * 220.47266f < 1000f)
		{
			position.y = 4.53571f;
		}
		else if (position.y > Ap._ceiling)
		{
			position.y = Ap._ceiling;
		}
		float num = Ap._machLimitCurve.Evaluate(position.y * 220.47266f);
		if (speedValueInMach > num)
		{
			speedValueInMach = num;
		}
		float interceptorSpeed = speedValueInMach * Atmosphere.SpeedOfSound(position.y * 67.200066f) * 0.0148809375f;
		(Vector3, bool) tuple = Utils.Intercept(base.transform.position, interceptorSpeed, targetObject.transform.position, targetObject._velocityVecInUnity);
		(interceptPosition, _) = tuple;
		if (!tuple.Item2)
		{
			return false;
		}
		return true;
	}

	public float BreakOffTimeSeconds()
	{
		float num = Mathf.Max(Ap._maxG - 1f, 1f) * 9.81f;
		return Mathf.Sqrt(120f / num) + 0.8f;
	}

	public bool CollisionWarning(ObjectBase targetObject)
	{
		if (targetObject == null)
		{
			return false;
		}
		if (Mathf.Abs(Utils.calculateYawToTarget(base.transform.forward, base.transform.position, targetObject.transform.position)) > 10f)
		{
			return false;
		}
		Vector3 vector = targetObject.transform.position - base.transform.position;
		Vector3 vector2 = base.UnityVelocityVector - targetObject.UnityVelocityVector;
		float sqrMagnitude = vector2.sqrMagnitude;
		if (sqrMagnitude < 1E-06f)
		{
			return false;
		}
		float num = Vector3.Dot(vector, vector2) / sqrMagnitude;
		if (num < 0f)
		{
			return false;
		}
		if ((vector - num * vector2).magnitude * 67.200066f > 60f)
		{
			return false;
		}
		return num <= BreakOffTimeSeconds();
	}

	public override void SetOpticalViewModelVisible(bool visible)
	{
		base.SetOpticalViewModelVisible(visible);
		SetOpticalViewAircraftArtifactsVisible(visible);
	}

	private void SetOpticalViewAircraftArtifactsVisible(bool visible)
	{
		if (visible)
		{
			RestoreOpticalViewAircraftArtifacts();
			return;
		}
		_opticalAircraftVisualsHidden = true;
		RememberGameObjectState(Ap?._particlesObject, active: false);
		IEnumerable<TrailRenderer> enumerable = Ap?._trailRenderers;
		foreach (TrailRenderer item in enumerable ?? Enumerable.Empty<TrailRenderer>())
		{
			if (!(item == null))
			{
				if (!_opticalHiddenTrailRendererStates.ContainsKey(item))
				{
					_opticalHiddenTrailRendererStates.Add(item, item.enabled);
				}
				item.enabled = false;
				item.Clear();
				TrailRendererScaler component = item.gameObject.GetComponent<TrailRendererScaler>();
				if (component != null)
				{
					component.resetTimer();
				}
			}
		}
		IEnumerable<TowedSystem> enumerable2 = _obp?._towedSystems;
		foreach (TowedSystem item2 in enumerable2 ?? Enumerable.Empty<TowedSystem>())
		{
			HideOpticalViewTowedSystemVisuals(item2);
		}
	}

	private void HideOpticalViewTowedSystemVisuals(TowedSystem towedSystem)
	{
		if (towedSystem == null)
		{
			return;
		}
		RememberGameObjectState(towedSystem._attachedObject, active: false);
		if (towedSystem._cable != null)
		{
			RememberGameObjectState(towedSystem._cable.getCableObject(), active: false);
		}
		LineRenderer lineRenderer = towedSystem._trailRenderer?._myLine;
		if (!(lineRenderer == null))
		{
			if (!_opticalHiddenLineRendererStates.ContainsKey(lineRenderer))
			{
				_opticalHiddenLineRendererStates.Add(lineRenderer, lineRenderer.enabled);
			}
			lineRenderer.enabled = false;
			lineRenderer.positionCount = 0;
		}
	}

	private void RememberGameObjectState(GameObject gameObject, bool active)
	{
		if (!(gameObject == null))
		{
			if (!_opticalHiddenGameObjectStates.ContainsKey(gameObject))
			{
				_opticalHiddenGameObjectStates.Add(gameObject, gameObject.activeSelf);
			}
			if (gameObject.activeSelf != active)
			{
				gameObject.SetActive(active);
			}
		}
	}

	private void RestoreOpticalViewAircraftArtifacts()
	{
		if (!_opticalAircraftVisualsHidden)
		{
			return;
		}
		foreach (KeyValuePair<TrailRenderer, bool> opticalHiddenTrailRendererState in _opticalHiddenTrailRendererStates)
		{
			if (!(opticalHiddenTrailRendererState.Key == null))
			{
				opticalHiddenTrailRendererState.Key.enabled = opticalHiddenTrailRendererState.Value;
			}
		}
		foreach (KeyValuePair<LineRenderer, bool> opticalHiddenLineRendererState in _opticalHiddenLineRendererStates)
		{
			if (!(opticalHiddenLineRendererState.Key == null))
			{
				opticalHiddenLineRendererState.Key.enabled = opticalHiddenLineRendererState.Value;
			}
		}
		foreach (KeyValuePair<GameObject, bool> opticalHiddenGameObjectState in _opticalHiddenGameObjectStates)
		{
			if (!(opticalHiddenGameObjectState.Key == null))
			{
				opticalHiddenGameObjectState.Key.SetActive(opticalHiddenGameObjectState.Value);
			}
		}
		_opticalHiddenTrailRendererStates.Clear();
		_opticalHiddenLineRendererStates.Clear();
		_opticalHiddenGameObjectStates.Clear();
		_opticalAircraftVisualsHidden = false;
	}

	public override void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.LoadStateFromFile(ini, sectionName, isCampaignStateSave);
		if (!isCampaignStateSave)
		{
			base.RangeInKm.Value = ini.readValue(sectionName, "RangeInKm", base.RangeInKm.Value);
			UpdateFuelConsumption(0f);
			base.SonoDeployState.Value = ini.readValue(sectionName, "SonoDeployState", base.SonoDeployState.Value);
		}
	}

	public override void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.SaveStateToFile(ini, sectionName, isCampaignStateSave);
		if (!isCampaignStateSave)
		{
			ini.writeValue(sectionName, "RangeInKm", base.RangeInKm.Value);
			ini.writeValue(sectionName, "SonoDeployState", base.SonoDeployState.Value.ToString());
		}
	}
}
