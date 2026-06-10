using System;
using System.Collections.Generic;
using System.Linq;
using SeapowerUI.ViewModels;
using UniRx;
using Unity.Profiling;
using UnityEngine;

namespace SeaPower;

public class Missile : WeaponBase
{
	internal abstract class State
	{
		protected Missile _missile;

		public abstract void takeAction(Missile missile);

		protected State(Missile missile)
		{
			_missile = missile;
		}
	}

	internal class Inactive : State
	{
		public Inactive(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._flightStage = FlightStage.None;
		}

		public override void takeAction(Missile missile)
		{
		}
	}

	internal class Launch : State
	{
		private Vector3 initialMoveOfLaunchPlatform;

		public Launch(Missile missile)
			: base(missile)
		{
			_missile = missile;
			if (missile._hasSubmunition)
			{
				missile._weaponInstance.initializeSubmunition(missile._weaponInstance);
			}
			missile._flightStage = FlightStage.Launch;
			missile._goActiveTime = 3600f * missile._ap._minLaunchRangeInMiles / missile._ap._maxVelocityInKnots;
			missile._targetLost = false;
			missile._airLaunched = false;
			missile._maxPitch = 90f;
			missile._insDriftValue = Globals._unityRandom.NextFloat() - 0.5f;
			missile._insDriftValue += 0.5f * (float)MathF.Sign(missile._insDriftValue);
			missile._currentKnownTargetPosition = missile._initialSetTargetPosition;
			missile._leadTarget = !missile._ap._forceDirectPoint;
			missile._motorPerformance = 1.02f - Globals._unityRandom.NextFloat() / 25f;
			missile._targetInterceptPosition = missile.AimPointGeoPosition;
			if (missile._launchPlatform != null)
			{
				initialMoveOfLaunchPlatform = missile._launchPlatform.getVelocityInKnots() * missile._launchPlatform.getRootGameObject().transform.forward;
				missile._airLaunched = missile._launchPlatform.IsAirUnit;
			}
			missile.CreateWaypoints();
			if (!missile._checkForWaterExit)
			{
				return;
			}
			if (!missile._isUnderwaterTrailEffectStarted)
			{
				missile._isUnderwaterTrailEffectStarted = true;
				missile._weaponInstance._mainMesh.SetActive(value: false);
				if (missile._weaponInstance._mainMeshCanister != null)
				{
					missile._weaponInstance._mainMeshForLaunch.SetActive(value: false);
					missile._weaponInstance._mainMeshCanister.SetActive(value: true);
				}
				else if (missile._weaponInstance._mainMeshForLaunch != null)
				{
					missile._weaponInstance._mainMeshForLaunch.SetActive(value: false);
				}
				missile._underwaterTrailEffect = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(missile._ap._underwaterTrailEffect, missile.transform));
				if (missile._underwaterTrailEffect != null)
				{
					missile._underwaterTrailEffect.transform.localPosition = missile._ap._underwaterTrailEffect._position;
					missile._underwaterTrailEffect.transform.localRotation = Quaternion.identity;
					if (missile._weaponInstance._propAudioSource != null)
					{
						missile._weaponInstance._propAudioSource.Play();
					}
				}
			}
			missile._currentPitch = Utils.WrapAngle(missile.transform.localEulerAngles.x);
			if (missile._currentPitch > -15f)
			{
				DM.I($"missile._currentPitch = {missile._currentPitch}");
				missile._performUnderwaterTurn = true;
			}
		}

		public override void takeAction(Missile missile)
		{
			if (missile._dt < 2f && missile._launchPlatform != null)
			{
				float num = 1f - missile._dt / 2f;
				missile._moveOfLaunchPlatform = initialMoveOfLaunchPlatform * num;
			}
			missile._moveOfLaunchPlatform *= 0.0076554087f * GameTime.fixedDeltaTime;
			if (missile._checkForWaterExit)
			{
				if (missile._dt > 2f)
				{
					DM.I("missile._checkForWaterExit && missile._dt > 2f");
					missile._weaponInstance._collidersObject.SetActive(value: true);
					if ((bool)missile._weaponInstance._proximityFuzeCollider)
					{
						missile._weaponInstance._proximityFuzeCollider.gameObject.SetActive(value: false);
					}
					missile.changeState(new GoToSurface(missile));
				}
				return;
			}
			if (missile._dt > missile._weaponInstance._resourcesMeshSwitchTime && !missile._launchObjectSwitched)
			{
				missile._launchObjectSwitched = true;
				missile._weaponInstance._mainMesh.SetActive(value: true);
				if ((bool)missile._weaponInstance._mainMeshForLaunch)
				{
					missile._weaponInstance._mainMeshForLaunch.SetActive(value: false);
				}
				if (missile._weaponInstance._mainMeshCanister != null)
				{
					missile._weaponInstance._mainMeshCanister.SetActive(value: false);
				}
				foreach (WeaponSubModel subModel in missile._weaponInstance._subModels)
				{
					if (subModel._type == WeaponSubModel.Type.AFTERBURNER)
					{
						subModel._subModel.gameObject.SetActive(value: true);
					}
				}
			}
			if (!missile._isBoosterEffectStarted)
			{
				missile.ScheduleFlightEffects();
			}
			if (missile._prevflightState == FlightStage.DropFromAircraft && !missile._isTargetAirborne)
			{
				if (missile._currentPitch > 3f)
				{
					missile.transform.Rotate(Vector3.left, 24f * GameTime.fixedDeltaTime, Space.Self);
				}
				else if (missile._currentPitch < -3f)
				{
					missile.transform.Rotate(Vector3.right, 24f * GameTime.fixedDeltaTime, Space.Self);
				}
			}
			if (missile._ap._performsSlightBallisticInitialFlight && missile._currentPitch < -10f)
			{
				missile.transform.Rotate(Vector3.right, 6f * GameTime.fixedDeltaTime, Space.Self);
			}
			if (missile._dt > missile._ap._initialFlightPhaseDuration)
			{
				if (missile.Waypoints.Count > 0 && !missile.Waypoints[0].WpStatus.Done)
				{
					missile._lastWaypointPosition = missile._geoPosition;
				}
				if (missile._currentPitch > -15f)
				{
					missile._performUnderwaterTurn = true;
				}
				missile._weaponInstance._collidersObject.SetActive(value: true);
				if ((bool)missile._weaponInstance._proximityFuzeCollider)
				{
					missile._weaponInstance._proximityFuzeCollider.gameObject.SetActive(value: false);
				}
				if (missile._checkForWaterExit)
				{
					missile.changeState(new GoToSurface(missile));
				}
				else
				{
					missile.changeState(new ToBearing(missile));
				}
			}
		}
	}

	internal class GoToSurface : State
	{
		public GoToSurface(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._flightStage = FlightStage.GoToSurface;
		}

		public override void takeAction(Missile missile)
		{
			if (missile._checkForWaterExit)
			{
				if (missile._performUnderwaterTurn && missile._currentPitch > -88f)
				{
					missile.transform.Rotate(Vector3.left, 24f * GameTime.fixedDeltaTime, Space.Self);
				}
				return;
			}
			if (missile._underwaterTrailEffect != null)
			{
				missile._underwaterTrailEffect.GetComponent<ParticleSystem>().Stop();
			}
			if (missile._weaponInstance._propAudioSource != null)
			{
				missile._weaponInstance._propAudioSource.Stop();
			}
			if (missile == Singleton<RenderPosition>.Instance.getSelectedObject())
			{
				Singleton<RenderPosition>.Instance.deselectObjectAndDetachCamera();
				Singleton<RenderPosition>.Instance.switchToObject(missile, performSmoothMove: false, keepCamRotation: true);
			}
			if (!missile._isBoosterEffectStarted)
			{
				missile._weaponInstance._mainMesh.SetActive(value: true);
				if (missile._weaponInstance._mainMeshForLaunch != null)
				{
					missile._weaponInstance._mainMeshForLaunch.SetActive(value: false);
				}
				if (missile._weaponInstance._mainMeshCanister != null)
				{
					missile._weaponInstance._mainMeshCanister.SetActive(value: false);
				}
				foreach (WeaponSubModel subModel in missile._weaponInstance._subModels)
				{
					if (subModel._type == WeaponSubModel.Type.AFTERBURNER)
					{
						subModel._subModel.gameObject.SetActive(value: true);
					}
				}
				missile.ScheduleFlightEffects(stopBoosterAudioFirst: true);
			}
			if (missile._ap._performsSlightBallisticInitialFlight && missile._dt < missile._ap._initialFlightPhaseDuration && missile._currentPitch < -10f)
			{
				missile.transform.Rotate(Vector3.right, 6f * GameTime.fixedDeltaTime, Space.Self);
			}
			if (missile._dt > missile._ap._initialFlightPhaseDuration)
			{
				missile._lostGuidanceTime = GameTime.time;
				missile.changeState(new ToBearing(missile));
			}
		}
	}

	internal class DropFromAircraft : State
	{
		public DropFromAircraft(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._flightStage = FlightStage.DropFromAircraft;
		}

		public override void takeAction(Missile missile)
		{
			if (missile._dt < missile._ap._dropDuration)
			{
				if (missile._currentPitch > 0.5f)
				{
					missile.transform.Rotate(Vector3.left, 4f * GameTime.fixedDeltaTime, Space.Self);
				}
				else if (missile._currentPitch < -0.5f)
				{
					missile.transform.Rotate(Vector3.right, 4f * GameTime.fixedDeltaTime, Space.Self);
				}
				if (missile._launchObjectSwitched || !(missile._dt > missile._weaponInstance._resourcesMeshSwitchTime))
				{
					return;
				}
				missile._weaponInstance._mainMesh.SetActive(value: true);
				missile._weaponInstance._mainMeshForLaunch.SetActive(value: false);
				if (missile._weaponInstance._mainMeshCanister != null)
				{
					missile._weaponInstance._mainMeshCanister.SetActive(value: false);
				}
				missile._launchObjectSwitched = true;
				{
					foreach (WeaponSubModel subModel in missile._weaponInstance._subModels)
					{
						if (subModel._type == WeaponSubModel.Type.AFTERBURNER)
						{
							subModel._subModel.gameObject.SetActive(value: true);
						}
					}
					return;
				}
			}
			missile._prevflightState = FlightStage.DropFromAircraft;
			missile.changeState(new Launch(missile));
		}
	}

	internal class ToBearing : State
	{
		private bool _vert;

		private bool _loftingSAM;

		private float _startTime;

		private float _originalAimPointAltitude;

		private bool _restoreAltitude;

		public ToBearing(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._flightStage = FlightStage.ToBearing;
			_startTime = GameTime.time;
			if (missile._isTargetAirborne)
			{
				float num = missile._geoPosition.UnityDistance(missile.AimPointGeoPosition);
				if (num > missile._ap._seaSkimmingStartDistToTargetUnity && num > missile._ap._finalFlightPhaseDistToTargetUnity && num > missile._ap._terminalApproachDist)
				{
					_loftingSAM = true;
				}
			}
			_originalAimPointAltitude = missile.AimPointGeoPosition.y;
		}

		public override void takeAction(Missile missile)
		{
			if (missile._isTargetAirborne && missile._airLaunched)
			{
				missile.changeState(new MaintainHeight(missile));
				return;
			}
			missile.isValidTarget(missile.CurrentIntendedTargetObject);
			if (Vector3.Angle(missile._aimPointGeoPosition.ToUnity() - missile.transform.position, missile.transform.forward) < 5f || GameTime.time - _startTime > 10f)
			{
				missile.changeState(new MaintainHeight(missile));
			}
		}
	}

	internal class MaintainHeight : State
	{
		private bool _finalFlightAltSet;

		private float _originalAimPointAltitude;

		private bool _restoreAltitude;

		public MaintainHeight(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._flightStage = FlightStage.MaintainHeight;
		}

		public override void takeAction(Missile missile)
		{
			if (missile.GroupWingman.Value)
			{
				Vector3 vector = missile._inGroupPosition.ToUnity();
				vector.y = missile.DesiredAltitude.Value;
			}
			missile._usingSubmunition = false;
			if (missile._ap._hasSubmunition && ((missile._ap._submunitionType == Ammunition.Type.Torpedo && (missile.CurrentIntendedTargetObject == null || missile.CurrentIntendedTargetObject._type == ObjectType.Submarine)) || missile.CurrentIntendedTargetObject.isSubmerged()))
			{
				missile._usingSubmunition = true;
			}
			Vector3 position = missile.transform.position;
			Vector3 vector2 = missile._targetInterceptPosition.ToUnity();
			vector2.y = position.y;
			float magnitude = (position - vector2).magnitude;
			if (missile.CurrentIntendedTargetObject != null && missile._isTargetAirborne && missile._seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
			{
				magnitude = (position - missile.CurrentIntendedTargetObject.transform.position).magnitude;
			}
			bool flag = true;
			if (missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				if (missile.CurrentIntendedTargetObject != null)
				{
					if (!missile.ConnectionLost.Value)
					{
						if (missile.isValidTarget(missile.CurrentIntendedTargetObject))
						{
							if (missile.getFlightStage() != FlightStage.MoveToLoftAlt)
							{
								missile.getFlightStage();
								_ = 7;
							}
							_originalAimPointAltitude = missile.AimPointGeoPosition.y;
						}
					}
					else if (!(missile._geoPosition.UnityDistance(missile.AimPointGeoPosition) < 1f))
					{
						_ = missile.AimPointGeoPosition.y;
						_ = missile._ap._maxLoftAltUnity;
					}
				}
				if (missile._seeker._guidanceType == AmmunitionParameters.GuidanceType.TVHoming)
				{
					_ = missile.ConnectionLost.Value;
				}
				if (missile._seeker._guidanceType != AmmunitionParameters.GuidanceType.None)
				{
					if (missile._flightStage != FlightStage.MaintainHeight && missile._ap._requiresTargetToProceed && !missile.GroupWingman.Value)
					{
						flag = false;
					}
					if (!missile._usingSubmunition && (missile._seeker._guidanceType == AmmunitionParameters.GuidanceType.TVHoming || magnitude < missile._ap._seekerActiveRange || magnitude < missile._ap._seekerPassiveRange))
					{
						if (!missile._seeker.isActive() && !missile.GroupWingman.Value)
						{
							missile._seeker.setActive(active: true);
							missile._lockOnCheckTime = GameTime.time;
						}
						else if (missile.CurrentIntendedTargetObject == null && magnitude < missile._ap._finalFlightPhaseDistToTargetUnity)
						{
							_ = missile.CurrentStrongestTargetEcho;
						}
					}
					if (missile._seeker.isActive())
					{
						if (missile.CurrentIntendedTargetObject != null)
						{
							flag = true;
						}
						else if ((!missile.IsPlayerObject || missile.ConnectionLost.Value) && GameTime.time - missile._seeker._activationTime > 180f && missile.CurrentStrongestTargetEcho != null)
						{
							missile.CurrentIntendedTargetObject = missile.CurrentStrongestTargetEcho._objectBase;
							flag = true;
						}
					}
				}
			}
			else if (missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.WireGuided && missile.CurrentIntendedTargetObject != null && missile.isValidTarget(missile.CurrentIntendedTargetObject))
			{
				Vector3 targetPosition = missile.CurrentIntendedTargetObject.transform.TransformPoint(missile._currentTargetPoint._position);
				missile.AimPointGeoPosition = Utils.CalculateInterceptGeoPosition(missile.transform.position, missile._velocityInUnity, targetPosition, missile.CurrentIntendedTargetObject.UnityVelocityVector, _missile._isTargetAirborne, _missile.EffectiveFOV);
			}
			if (!missile._fuseActive && magnitude < 27.559496f)
			{
				missile._fuseActive = true;
			}
			int num = 0;
			switch (missile._flightStage)
			{
			case FlightStage.MaintainLoftAlt:
				num = 1;
				break;
			case FlightStage.MaintainSeaSkimming:
				num = 2;
				break;
			case FlightStage.MaintainFinalFlightAlt:
				num = 3;
				break;
			}
			bool flag2 = missile._ap._useFinalFlightInLandMode || missile._seeker._mode != Ammunition.SeekerMode.LandUnit;
			if (magnitude < missile._ap._terminalApproachDist && (flag || missile._usingSubmunition))
			{
				float num2 = missile._ap._terminalDelay;
				if (num2 < 0f)
				{
					num2 = missile._goActiveTime * 0.8f;
				}
				if (GameTime.time - missile._launchTime < num2)
				{
					if (flag2 && missile._ap._finalFlightPhaseDistToTargetUnity > 0f)
					{
						if (missile.GroupLeader)
						{
							missile.DesiredAltitude.Value = missile._ap._groupLeaderAltUnity;
						}
						else if (missile.GroupWingman.Value)
						{
							missile.DesiredAltitude.Value = missile._ap._groupWingmanAltUnity;
						}
						else
						{
							if (_finalFlightAltSet)
							{
								return;
							}
							_finalFlightAltSet = true;
							if (missile._ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Maintain)
							{
								missile._ap._finalFlightPhaseAltUnity = missile.transform.position.y;
							}
							else if (missile._ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Target)
							{
								if (missile.CurrentIntendedTargetObject != null)
								{
									missile._ap._finalFlightPhaseAltUnity = missile.CurrentIntendedTargetObject.transform.position.y;
								}
								else
								{
									missile._ap._finalFlightPhaseAltUnity = missile.transform.position.y;
								}
							}
							else if (missile._ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Launcher && missile._launchPlatform != null)
							{
								missile._ap._finalFlightPhaseAltUnity = missile._launchPlatform.transform.position.y;
							}
						}
					}
					else
					{
						_ = missile._ap._seaSkimmingStartDistToTargetUnity;
						_ = 0f;
					}
					return;
				}
				if (missile._usingSubmunition)
				{
					missile.DesiredAltitude.Value = missile._ap._seaSkimmingSubAltUnity;
					if (missile._ap._submunitionType == Ammunition.Type.Torpedo)
					{
						if (missile._submunitionDropped)
						{
							Singleton<ObjectsManager>.Instance.removeObject(missile);
							missile.changeState(new SelfDestruct(missile));
						}
						else if (magnitude < missile._ap._submunitionSeekerRange + 2f)
						{
							missile.DropTorpedo();
							return;
						}
					}
				}
				else
				{
					bool flag3 = true;
					if (!missile._wasInMissileGroup)
					{
						if (missile.GroupWingman.Value)
						{
							flag3 = false;
						}
						else
						{
							if (!missile._seeker.isActive())
							{
								missile._seeker.setActive(active: true);
								missile._lockOnCheckTime = GameTime.time;
								if (missile.GroupLeader)
								{
									if (!missile._missileGroup._lockOnChecked)
									{
										missile._missileGroup._lockOnChecked = true;
										missile._missileGroup._lockOnCheckTime = missile._lockOnCheckTime;
									}
									else
									{
										missile._lockOnCheckTime = missile._missileGroup._lockOnCheckTime;
									}
								}
							}
							if (missile._ap._searchForTargetsTime > 0f && missile._seeker._guidanceType != AmmunitionParameters.GuidanceType.None)
							{
								if (missile._seeker._strongestSensorEcho == null)
								{
									missile._lockOnCheckTime = GameTime.time;
								}
								if (GameTime.time - missile._lockOnCheckTime < missile._ap._searchForTargetsTime)
								{
									flag3 = false;
								}
								if (missile.GroupLeader && (magnitude < missile._ap._seekerActiveRange * 0.6f || magnitude < missile._ap._seekerPassiveRange * 0.6f))
								{
									missile._seeker.isInSensorFOV(missile.transform.position, missile.transform.forward, vector2);
									flag3 = true;
								}
							}
						}
					}
					if (flag3)
					{
						if (missile._ap.SearchModeSetting.Value != AmmunitionParameters.SearchMode.None)
						{
							missile.changeState(new EnterSearchMode(missile));
						}
						else
						{
							missile.changeState(new TerminalApproach(missile));
						}
					}
				}
			}
			else if (flag2 && magnitude < missile._ap._finalFlightPhaseDistToTargetUnity && flag && num <= 3)
			{
				if (!_finalFlightAltSet)
				{
					_finalFlightAltSet = true;
					if (missile._ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Maintain)
					{
						missile._ap._finalFlightPhaseAltUnity = missile.transform.position.y;
					}
					else if (missile._ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Launcher && missile._launchPlatform != null)
					{
						missile._ap._finalFlightPhaseAltUnity = missile._launchPlatform.transform.position.y;
					}
					if (missile._ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Target)
					{
						if (missile.CurrentIntendedTargetObject != null)
						{
							missile._ap._finalFlightPhaseAltUnity = missile.CurrentIntendedTargetObject.transform.position.y;
						}
						else
						{
							missile._ap._finalFlightPhaseAltUnity = missile.transform.position.y;
						}
					}
				}
				missile._flightStage = FlightStage.MaintainFinalFlightAlt;
			}
			else if (magnitude < missile._ap._seaSkimmingStartDistToTargetUnity && flag && num <= 2 && (missile._ap._loftToSkim || missile._flightStage != FlightStage.MaintainLoftAlt))
			{
				missile._flightStage = FlightStage.MaintainSeaSkimming;
			}
			else if (num <= 1)
			{
				missile._flightStage = FlightStage.MaintainLoftAlt;
			}
			if (missile._flightStage == FlightStage.MaintainFinalFlightAlt)
			{
				if (missile.GroupLeader)
				{
					missile.DesiredAltitude.Value = missile._ap._groupLeaderAltUnity;
				}
				else if (missile.GroupWingman.Value)
				{
					missile.DesiredAltitude.Value = missile._ap._groupWingmanAltUnity;
				}
			}
			else
			{
				_ = missile._flightStage;
				_ = 9;
			}
		}
	}

	internal class EnterSearchMode : State
	{
		public EnterSearchMode(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._maintainingHeight = true;
			missile._flightStage = FlightStage.EnterSearchMode;
			missile._stageStartTime = GameTime.time;
		}

		public override void takeAction(Missile missile)
		{
			if (missile._seeker._strongestSensorEcho == null)
			{
				switch (missile._ap.SearchModeSetting.Value)
				{
				case AmmunitionParameters.SearchMode.None:
					missile.changeState(new TerminalApproach(missile));
					break;
				case AmmunitionParameters.SearchMode.Snake:
					missile.changeState(new PerformSnakeSearch(missile));
					break;
				case AmmunitionParameters.SearchMode.Circle:
					missile.changeState(new TerminalApproach(missile));
					break;
				case AmmunitionParameters.SearchMode.Spiral:
					missile.changeState(new TerminalApproach(missile));
					break;
				case AmmunitionParameters.SearchMode.Ladder:
					missile.changeState(new TerminalApproach(missile));
					break;
				}
			}
			else
			{
				missile.changeState(new TerminalApproach(missile));
			}
		}
	}

	internal class PerformSnakeSearch : State
	{
		private float _initiaHeading;

		private float _heightToMaintain;

		public PerformSnakeSearch(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._maintainingHeight = true;
			missile._flightStage = FlightStage.PerformSnakeSearch;
			missile._stageStartTime = GameTime.time;
			_initiaHeading = missile.transform.eulerAngles.y;
			_heightToMaintain = missile.transform.position.y;
		}

		public override void takeAction(Missile missile)
		{
			if (missile._seeker._strongestSensorEcho == null)
			{
				float f = MathF.PI * 2f * (GameTime.time - missile._stageStartTime) / missile._ap._snakeSearchTurnCycleTime;
				float num = missile._ap._snakeSearchAngle * Mathf.Sin(f);
				missile.transform.rotation = Quaternion.Euler(missile.transform.eulerAngles.x, _initiaHeading + num, missile.transform.eulerAngles.z);
				missile.transform.localEulerAngles = new Vector3(missile.transform.localEulerAngles.x, missile.transform.localEulerAngles.y, 0f);
				missile.maintainHeight(_heightToMaintain);
			}
			else
			{
				missile.changeState(new TerminalApproach(missile));
			}
		}
	}

	internal class SelfDestruct : State
	{
		public SelfDestruct(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._maintainingHeight = false;
			missile._flightStage = FlightStage.SelfDestruct;
			missile._stageStartTime = GameTime.time;
		}

		public override void takeAction(Missile missile)
		{
			if (GameTime.time - missile._stageStartTime > 2f)
			{
				missile.setHeight(-100f, GameTime.time - missile._stageStartTime);
			}
			if (GameTime.time - missile._stageStartTime > 20f)
			{
				DM.Log(missile.FullIDData() + " is destroyed by time limit of SelfDestruct state", DM.EventLogFilters.Destruction);
				missile.Destruction(missile.transform.position, missile.transform.rotation, null);
			}
		}
	}

	internal class TerminalApproach : State
	{
		private bool _afterGroupAltitudeCorrection;

		private float _minAllowedAltitude = float.MaxValue;

		private bool _stickToAltitude = true;

		private float _teminalDiveDistanceSq;

		private bool _inDiveRange;

		private float _maxBearingToTarget = 90f;

		private float _terminalStartTime;

		private Vector3 _localOffset = Vector3.zero;

		private ObjectBase _currentTarget;

		public TerminalApproach(Missile missile)
			: base(missile)
		{
			_missile = missile;
			_missile._flightStage = FlightStage.TerminalApproach;
			_terminalStartTime = GameTime.time;
			_missile._targetLost = false;
			_missile._interceptionData.Clear();
			_missile._maintainingHeight = false;
			_teminalDiveDistanceSq = _missile._ap._terminalDiveDistance * _missile._ap._terminalDiveDistance;
			if (missile._isTargetAirborne || missile._ap._targetType == Ammunition.Target.AAW)
			{
				_minAllowedAltitude = -1000f;
				_stickToAltitude = false;
			}
			if (_missile._ap._terminalAltMode == AmmunitionParameters.LoftMode.Launcher && _missile._launchPlatform != null)
			{
				_minAllowedAltitude = _missile._launchPlatform.transform.position.y;
			}
			else if (_missile._ap._terminalAltMode == AmmunitionParameters.LoftMode.Target && _missile.CurrentTarget != null)
			{
				_minAllowedAltitude = _missile.CurrentTarget.transform.position.y;
			}
			else if (_missile._ap._terminalAltMode == AmmunitionParameters.LoftMode.Maintain)
			{
				_minAllowedAltitude = _missile.transform.position.y;
			}
			else if (missile._ap._terminalAltUnity > 0f)
			{
				_minAllowedAltitude = _missile._ap._terminalAltUnity;
			}
			else
			{
				bool num = _missile._ap._useFinalFlightInLandMode || _missile._seeker._mode != Ammunition.SeekerMode.LandUnit;
				if (_minAllowedAltitude > _missile._ap._maxLoftAltUnity && _missile._ap._maxLoftAltUnity > 0f)
				{
					_minAllowedAltitude = _missile._ap._maxLoftAltUnity;
				}
				if (_minAllowedAltitude > _missile._ap._seaSkimmingAltUnity && _missile._ap._seaSkimmingAltUnity > 0f)
				{
					_minAllowedAltitude = _missile._ap._seaSkimmingAltUnity;
				}
				if (num && _minAllowedAltitude > _missile._ap._finalFlightPhaseAltUnity && _missile._ap._finalFlightPhaseAltUnity > 0f)
				{
					_minAllowedAltitude = _missile._ap._finalFlightPhaseAltUnity;
				}
				_stickToAltitude = false;
			}
			if (_minAllowedAltitude == float.MaxValue)
			{
				_minAllowedAltitude = 0f;
			}
			if (_missile._ap._guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming)
			{
				_minAllowedAltitude = 0f;
				_stickToAltitude = false;
			}
			_missile._ap._currentMinFlightAltUnity = _minAllowedAltitude;
			if (!missile._fuseActive)
			{
				missile._fuseActive = true;
			}
			if (missile._ap._localTerminalOnly)
			{
				missile._onWire = false;
				missile.ConnectionLost.Value = true;
				missile.ConnectionLostForever.Value = true;
				missile._guidingRadarSystem?.FreeChannels(missile.CurrentIntendedTargetObject, missile);
			}
			if (missile.GroupLeader)
			{
				_afterGroupAltitudeCorrection = true;
				missile._missileGroup.CashIn();
			}
			foreach (SensorSystem sensor in missile._sensors)
			{
				sensor.Enable();
			}
		}

		public void OffsetTargetPosition()
		{
			_localOffset = Vector3.zero;
			if (_currentTarget._type == ObjectType.Vessel || _currentTarget._type == ObjectType.Submarine)
			{
				Compartments compartments = _currentTarget.Compartments;
				float y = _currentTarget._obp._floodingCompartmentsHeight / 2f;
				double num = 0.0;
				for (int i = 0; i < 11; i++)
				{
					int num2 = compartments._portCompartments.Length - 1;
					if (num2 >= 0)
					{
						float num3 = Mathf.Abs((compartments._portCompartments[0]._position - compartments._portCompartments[num2]._position) / 2f) / 2f;
						num = TMAUtils.GenerateRandomVariant(0.0, num3, ref Globals._unityRandom);
						if (num < (double)compartments._portCompartments[0]._position && num > (double)compartments._portCompartments[num2]._position)
						{
							break;
						}
					}
				}
				_localOffset = new Vector3(0f, y, (float)num);
			}
			else if (_currentTarget._type == ObjectType.LandUnit && _missile._ap.GuidanceTypeForTarget(_missile.CurrentIntendedTargetObject) != AmmunitionParameters.GuidanceType.AntiRadiationHoming)
			{
				LandUnitParameters.TargetPoint targetPoint = Utils.AssignLandUnitTargetPoint(_currentTarget, excludeDestroyedSystems: false);
				_missile._currentTargetPointDebug = targetPoint;
				_localOffset = targetPoint._position;
				if (_localOffset == Vector3.zero)
				{
					_localOffset.y = 0.015f;
				}
				_missile._currentTargetPoint = targetPoint;
			}
		}

		public override void takeAction(Missile missile)
		{
			if (_afterGroupAltitudeCorrection)
			{
				_afterGroupAltitudeCorrection = false;
			}
			missile._maintainingHeight = false;
			if (missile.CurrentStrongestTargetEcho != null)
			{
				UpdateTargetPositionFromSeeker();
			}
			if (missile._onWire || missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				if (!missile.ConnectionLost.Value)
				{
					if (missile.isValidTarget(missile.CurrentIntendedTargetObject))
					{
						if (_currentTarget != missile.CurrentIntendedTargetObject)
						{
							_currentTarget = missile.CurrentIntendedTargetObject;
							OffsetTargetPosition();
						}
						if (missile._seeker._guidanceType == AmmunitionParameters.GuidanceType.TVHoming)
						{
							missile.AimPointGeoPosition = missile.AimPointGeoPosition.OffsetVector(missile._deviationVector * missile._TVRCdeviationMagnitude) * Time.fixedDeltaTime * 10f;
						}
					}
				}
				else if (missile.CurrentStrongestTargetEcho != null)
				{
				}
			}
			else
			{
				_ = missile.CurrentStrongestTargetEcho;
			}
			ObjectBase currentTarget = missile.CurrentTarget;
			if (currentTarget != null && currentTarget != missile._lastTargetObject && currentTarget._type != ObjectType.Chaff)
			{
				missile._lastTargetObject = currentTarget;
				missile.CalculateDeviation();
			}
			CalculateDeviation();
			Vector3 vector = _missile.AimPointGeoPosition.ToUnity();
			if (!_inDiveRange)
			{
				_inDiveRange = (_missile.transform.position - vector).sqrMagnitude < _teminalDiveDistanceSq;
			}
			if (missile._ap._guidanceType != AmmunitionParameters.GuidanceType.None && !_missile._isTargetAirborne)
			{
				_ = _inDiveRange;
			}
			float num = Mathf.Abs(Utils.calculateYawToTarget(missile.transform.forward, missile._geoPosition, missile.AimPointGeoPosition));
			if (num < _maxBearingToTarget || GameTime.time - _terminalStartTime < 5f)
			{
				ApproachTarget();
			}
			else if (!missile._targetLost)
			{
				missile._targetLost = true;
				missile._targetLostTime = GameTime.time;
				missile._lostGuidanceTime = -1f;
			}
			if (missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand && missile.ConnectionLost.Value && missile.NoSeekerActive())
			{
				_ = _maxBearingToTarget;
				_ = missile.AimPointGeoPosition.y;
				_ = missile._ap._maxLoftAltUnity;
			}
			CheckSelfDestruct();
		}

		private void UpdateTargetPositionFromSeeker()
		{
			_missile._didSeekerInitiallyPickUpTarget = true;
			_missile._seeker.getEstimatedTargetVelocityVector();
			_missile._seeker.getEstimatedTargetPosition();
			if (_missile._ap._guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming && _missile.CurrentStrongestTargetEcho._objectBase != null && _missile.CurrentStrongestTargetEcho._objectBase is LandUnit)
			{
				Vector3 position = _missile._currentTargetPoint._position;
				if (position == Vector3.zero)
				{
					position.y = 0.015f;
				}
				_missile.CurrentStrongestTargetEcho._objectBase.transform.TransformPoint(position);
			}
			if (_missile?.CurrentStrongestTargetEcho?._objectBase != null)
			{
				_ = _missile.CurrentStrongestTargetEcho._objectBase is Vessel;
			}
		}

		private void CalculateDeviation()
		{
			_missile._finalDeviationMagnitude = 0f;
			ObjectBase currentTarget = _missile.CurrentTarget;
			_missile._finalDeviationMagnitude += _missile._CEPdeviationMagnitude;
			if (_missile._seeker?._lockedOnTarget == null && _missile._seeker is RadiationSeeker radiationSeeker)
			{
				_missile._finalDeviationMagnitude += radiationSeeker._armSeekerDeviation;
			}
			if (_missile._deviationMagnitudeWithJam > 0f)
			{
				_missile._finalDeviationMagnitude += _missile._deviationMagnitudeWithJam;
				if (_missile._seeker.isActive())
				{
					_missile._seeker.setActive(active: false);
					_missile._seeker._strongestSensorEcho = null;
					_missile._seeker._potentialContacts.Clear();
					_missile._seeker._contactsInRange.Clear();
					_missile._ignoreCollisions = true;
					_missile.ConnectionLost.Value = true;
					_missile.ConnectionLostForever.Value = true;
					_missile._fuseActive = false;
					if (_missile._isTargetAirborne)
					{
						_missile._targetLost = true;
						_missile._targetLostTime = GameTime.time - _missile._ap._selfDestructDelay + 8.5f;
					}
				}
			}
			else if (currentTarget != null)
			{
				if (Globals._testIsUnitDefenseActive && (currentTarget.transform.position - _missile.transform.position).magnitude < 82.67849f)
				{
					_missile._deviationMagnitudeWithJam = 0f;
					TrulyObservableCollection<SensorSystem> trulyObservableCollection = null;
					if (currentTarget._obp != null)
					{
						trulyObservableCollection = currentTarget._obp._sensorSystems;
					}
					else if (currentTarget is WeaponBase weaponBase)
					{
						trulyObservableCollection = weaponBase._sensors;
					}
					if (trulyObservableCollection != null)
					{
						foreach (SensorSystem item in trulyObservableCollection)
						{
							if (!(item is SensorSystemECM sensorSystemECM) || (sensorSystemECM._ecm._ep._type != ECMParameters.ECMType.Defensive && sensorSystemECM._ecm._ep._type != ECMParameters.ECMType.Universal))
							{
								continue;
							}
							if (item.IsOn.Value)
							{
								if (sensorSystemECM._ecm.jamWeapon(_missile))
								{
									break;
								}
							}
							else if (!(currentTarget is WeaponBase))
							{
								sensorSystemECM.Enable();
							}
						}
					}
					_missile._finalDeviationMagnitude += _missile._deviationMagnitudeWithJam;
				}
				if (_missile._isTargetAirborne && _missile._interceptionData.ContainsKey(currentTarget) && !_missile._interceptionData[currentTarget])
				{
					_missile._finalDeviationMagnitude += _missile._ap._fuzeProximityDistance + 0.29761875f;
					if (currentTarget is Aircraft aircraft)
					{
						_missile._finalDeviationMagnitude += 0.14880937f;
						if (aircraft.Ap._isLarge)
						{
							_missile._finalDeviationMagnitude += 0.29761875f;
						}
					}
				}
			}
			if (_missile._isTargetAirborne)
			{
				if (DM._samOverride == 1)
				{
					_missile._finalDeviationMagnitude = 0f;
				}
				if (currentTarget != null && _missile._deviationVector.y < 0f && _missile._finalDeviationMagnitude < _missile._ap._fuzeProximityDistance && currentTarget.AltitudeOverClutter < _missile._ap._fuzeProximityDistance * 2f)
				{
					_missile._deviationVector.y = 0f - _missile._deviationVector.y;
				}
			}
			else if (DM._asmOverride == 1)
			{
				_missile._finalDeviationMagnitude = 0f;
			}
			Vector3 vector = (_missile.transform.right * _missile._deviationVector.x + _missile.transform.up * _missile._deviationVector.y).normalized;
			if (currentTarget != null)
			{
				if (currentTarget is Vessel)
				{
					vector = Vector3.down;
				}
				else if (currentTarget is LandUnit)
				{
					vector = (Vector3.right * _missile._deviationVector.x + Vector3.forward * _missile._deviationVector.z).normalized;
				}
			}
			_missile.AimPointGeoPosition = _missile.AimPointGeoPosition.OffsetVector(vector * _missile._finalDeviationMagnitude);
		}

		private void ApproachTarget()
		{
			float num = (float)_missile._geoPosition.GetDistance(_missile.AimPointGeoPosition);
			ObjectBase currentTarget = _missile.CurrentTarget;
			if (currentTarget != null && num < 55.11899f && currentTarget is Aircraft aircraft && aircraft._missileToEvade == null)
			{
				aircraft._missileToEvade = _missile;
			}
			if (_missile._isTargetAirborne && num < 27.559496f && currentTarget != null && currentTarget._type != ObjectType.Chaff && !_missile._interceptionData.ContainsKey(currentTarget))
			{
				if (currentTarget.IsDestroyed)
				{
					_missile._interceptionData.Add(currentTarget, value: true);
				}
				else if (_missile._deviationMagnitudeWithJam > 0.0001f)
				{
					_missile._interceptionData.Add(currentTarget, value: false);
				}
				else if (_missile._requiresExternalGuidance && _missile._seeker?._lockedOnTarget == null && _missile._lostGuidanceTime - GameTime.time > 0.01f)
				{
					_missile._interceptionData.Add(currentTarget, value: false);
				}
				else
				{
					_missile._interceptionData.Add(currentTarget, Utils.CalculateInterceptionChance(currentTarget, _missile, null, (_missile._launchPlatform != null) ? _missile._launchPlatform.getUIDAndName() : ""));
				}
				if (!_missile._interceptionData[currentTarget])
				{
					_missile.ConnectionLostForever.Value = true;
				}
			}
			if (!_missile._hasPoppedUp && _missile.CurrentStrongestTargetEcho != null)
			{
				if (_missile._seeker._lockedOnCountermeasure != null || _missile._deviationMagnitudeWithJam > 0.0001f)
				{
					_missile._hasPoppedUp = true;
					_missile._poppingUp = false;
				}
				float num2 = _missile._ap._terminalPopUpDist;
				if (_missile._ap._selectivePopUp)
				{
					if (_missile.CurrentStrongestTargetEcho._objectBase is Vessel vessel)
					{
						if (vessel.VP._displacement > 420f)
						{
							num2 = -1f;
						}
					}
					else if (!(_missile.CurrentStrongestTargetEcho._objectBase is Submarine))
					{
						num2 = -1f;
					}
				}
				if (num < num2)
				{
					_missile._poppingUp = true;
					if (_missile._maintainingHeight)
					{
						_missile._maintainingHeight = false;
					}
					if (_missile.transform.position.y > _missile._ap._terminalPopUpHeight)
					{
						_missile._hasPoppedUp = true;
						_missile._poppingUp = false;
						_inDiveRange = true;
					}
				}
				else
				{
					CheckAltitude();
				}
			}
			else
			{
				CheckAltitude();
			}
		}

		private void CheckAltitude()
		{
			if (!_inDiveRange && _missile._seeker._lockedOnCountermeasure == null && _missile._seeker._mode == Ammunition.SeekerMode.LandUnit && _missile._ap._terrainFollowFlight)
			{
				if (_missile.AltitudeOverClutter < _minAllowedAltitude)
				{
					_stickToAltitude = true;
				}
				else if (TerrainUtils.GetSafeAltitudeOverSurface(_missile, _missile.transform.position.y, _minAllowedAltitude, cantStayAtAltitude: false, isWeapon: true) > _missile.transform.position.y)
				{
					_stickToAltitude = true;
				}
			}
			else
			{
				_stickToAltitude = false;
			}
		}

		private void CheckSelfDestruct()
		{
			if (!_missile._targetLost && _missile._isTargetAirborne && (_missile._ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming || (_missile._ap._guidanceType == AmmunitionParameters.GuidanceType.None && _missile._ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)) && (_missile.ConnectionLostForever.Value || (GameTime.time - _terminalStartTime > 3f && _missile.CurrentTarget == null)))
			{
				_missile._targetLost = true;
				_missile._targetLostTime = GameTime.time;
				if (_missile._guidingRadarSystem != null)
				{
					_missile._guidingRadarSystem.FreeChannels(_missile.CurrentIntendedTargetObject, _missile);
				}
			}
			if (_missile._targetLost && (_missile._seeker._guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming || !_missile._ap._targetMemory))
			{
				if (_missile.CurrentTarget != null && _missile._seeker.isActive() && _missile._seeker._firstLockedOnTarget != null)
				{
					_missile._targetLost = false;
				}
				else if (GameTime.time - _missile._targetLostTime > _missile._ap._selfDestructDelay + 2f && !_missile.IsDestroyed)
				{
					DM.I(_missile.getName() + " self-destructed due to target lost");
					DM.Log(_missile.FullIDData() + " is self-destructed due to target lost", DM.EventLogFilters.Destruction);
					_missile.Destruction(_missile.transform.position, _missile.transform.rotation, null);
				}
			}
		}
	}

	internal class Debris : State
	{
		public Debris(Missile missile)
			: base(missile)
		{
			_missile = missile;
			missile._flightStage = FlightStage.Debris;
		}

		public override void takeAction(Missile missile)
		{
			Vector3 vector = Vector3.down * missile._debrisDownSpeed;
			Vector3 vector2 = missile._debrisSpeed * missile._debrisMoveDirection + vector;
			missile._weaponInstance._debrisMesh.transform.rotation = Quaternion.Slerp(missile._weaponInstance._debrisMesh.transform.rotation, Quaternion.LookRotation(vector2.normalized), 60f);
			missile._weaponInstance._debrisMesh.transform.Translate(missile._debrisSpeed * missile._debrisMoveDirection, Space.World);
			missile._weaponInstance._debrisMesh.transform.Translate(missile._debrisDownSpeed * Vector3.down, Space.World);
			missile._debrisSpeed *= missile._debrisVelocityDampening;
			missile._debrisDownSpeed += missile._debrisHeightLossAdd;
			if (GameTime.time - missile._debrisStartTime > missile._debrisLifeTime)
			{
				missile.changeState(new Terminated(missile));
			}
			if (!(missile._weaponInstance._debrisMesh.transform.position.y < 0f))
			{
				return;
			}
			if (missile._debrisFireEffect != null)
			{
				ParticleSystem[] componentsInChildren = missile._debrisFireEffect.GetComponentsInChildren<ParticleSystem>();
				foreach (ParticleSystem particleSystem in componentsInChildren)
				{
					if (particleSystem.name.ToLower() == "fireemitter")
					{
						particleSystem.Stop();
					}
					if (particleSystem.name.ToLower() == "helicopterfireeffect_light_fire")
					{
						particleSystem.Stop();
					}
				}
				if (missile._debrisFireEffect.transform.parent != null && missile._debrisFireEffect.transform.parent.tag != "PoolManager")
				{
					missile._debrisFireEffect.transform.SetParent(null);
				}
				missile._debrisFireEffect.transform.Translate(-0.5f * missile._debrisFireEffect.transform.up);
				ParticleAutoDestroy particleAutoDestroy = ParticleUtils.SetAutoDestroyAndCustomWorldSpace(missile._debrisFireEffect);
				if (particleAutoDestroy != null)
				{
					particleAutoDestroy.setTimer(8f);
				}
			}
			if (Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(missile._ap._debrisSplashEffect, null, missile._weaponInstance._debrisMesh.transform.position)) == null)
			{
				Log.Error("[" + missile._ap._ammunitionFileName + "]: failed to spawn debris splash effect", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Missile.cs", 2253, "takeAction");
			}
			missile.changeState(new Terminated(missile));
		}
	}

	internal class Terminated : State
	{
		public Terminated(Missile missile)
			: base(missile)
		{
			DM.I($"{missile.getUIDAndName()} Terminated state. FS = {missile._flightStage}");
			_missile = missile;
			missile._flightStage = FlightStage.Terminated;
		}

		public override void takeAction(Missile missile)
		{
			if ((bool)missile._testObject)
			{
				UnityEngine.Object.Destroy(missile._testObject);
			}
			missile.destroyObject(keepInFlightEffectAlive: true, score: false);
		}
	}

	public struct WaypointConfig
	{
		public float DistanceToTarget;

		public Waypoint.Settings Settings;
	}

	public class WaypointCreationContext
	{
		public AmmunitionParameters Ap;

		public bool IsAirTarget;

		public bool TerrainFollow;

		public bool IsGroupLeader;

		public float StartAngle;

		public bool SelectivePopUpDisabled;

		public float LaunchAltUnity;

		public float LoftAltUnityOverride = -1f;
	}

	private class SimulationWaypointState
	{
		public List<Waypoint> Waypoints;

		public GeoPosition LastWaypointPosition;
	}

	public class KinematicRangeResult : IComparable<KinematicRangeResult>
	{
		public float MaxRange;

		public float DistPercent;

		public float InterceptSpeed;

		public float InterceptTime;

		public bool LoftTooHigh;

		public bool TargetTooClose;

		public bool InRange
		{
			get
			{
				if (InterceptTime >= 0f)
				{
					return !LoftTooHigh;
				}
				return false;
			}
		}

		public int CompareTo(KinematicRangeResult other)
		{
			bool inRange = InRange;
			bool inRange2 = other.InRange;
			if (inRange != inRange2)
			{
				if (!inRange)
				{
					return -1;
				}
				return 1;
			}
			return other.InterceptTime.CompareTo(InterceptTime);
		}
	}

	private enum MissileEffectType
	{
		Booster,
		InFlight,
		Sustainer
	}

	private class MissileFlightEffect
	{
		public Effect effectDef;

		public MissileEffectType type;

		public float startDelay;

		public float endDelay;

		public bool started;

		public bool detached;

		public GameObject instance;

		public float startTime;
	}

	public struct FlightProfileContext(Missile missile, FlightStage stage)
	{
		public FlightStage Stage = stage;

		public AmmunitionParameters Ap = missile._ap;

		public Vector3 MissilePosition = missile.transform.position;

		public Vector3 LaunchPosition = missile._launchPosition.ToUnity();

		public float TraveledDistance = missile._traveledDistance;

		public bool UsingSubmunition = missile._usingSubmunition;

		public bool GroupLeader = missile.GroupLeader;

		public bool GroupWingman = missile.GroupWingman.Value;

		public bool CruiseMissile = missile._ap.Kinematics == AmmunitionParameters.KinematicsLevel.None;

		public float? TargetAltitude = missile._currentKnownTargetPosition.y;

		public float? SeekerFOV = ((missile._seeker != null && missile._seeker.isActive()) ? new float?(missile.EffectiveFOV) : ((float?)null));

		public bool SeekerActive = missile._seeker != null && missile._seeker.isActive();

		public bool IgnoreSeekerFov = missile._hasPoppedUp;

		public bool IsAirTarget = missile._isTargetAirborne;
	}

	private struct MissileTargetAltitude
	{
		public float? FlightAltitude;

		public float TargetAltutude;
	}

	private static readonly ProfilerMarker _profTakeAction = new ProfilerMarker("Missile: takeAction");

	private static readonly ProfilerMarker _profMoveForward = new ProfilerMarker("Missile: performMoveForward");

	public float _CEPdeviationMagnitude;

	public float _deviationMagnitudeWithAltitudeDebuff;

	public float _finalDeviationMagnitude;

	public float _currentPitch;

	private float _prevPitch;

	public float _birthTime;

	public float _goActiveTime = 15f;

	public bool _performUnderwaterTurn;

	private GameObject _testObject;

	private MeshRenderer _testObjectMeshRenderer;

	private TrailRenderer _waypointPathRenderer;

	public bool _poppingUp;

	public bool _hasPoppedUp;

	public bool _wasSpoofed;

	public bool _submunitionDropped;

	public bool _airLaunched;

	private IDisposable _tuneWaypointsDisposable;

	public float _currentRadarHorizon;

	public float _lockOnCheckTime;

	private float _lastRayDebugLogTime;

	public bool _usingSubmunition;

	public float _pitchAngle = 999999f;

	public bool _inMissileGroup;

	public bool _wasInMissileGroup;

	public MissileGroup _missileGroup;

	public float _inGroupVelocityInKnots;

	private float _insDriftValue;

	public GeoPosition _inGroupPosition;

	public Vector3 _inGroupOffset;

	public new GeoPosition _targetInterceptPosition;

	public float _motorPerformance;

	public bool _leadTarget;

	public Vector3 _lastKnownTargetVelocity;

	private float _startSnakeSearchTime;

	private RaycastHit[] _hitAll = new RaycastHit[500];

	public Dictionary<ObjectBase, bool> _interceptionData = new Dictionary<ObjectBase, bool>();

	public (Taskforce taskforce, ObjectType type) _currentIntendedTargetData = (taskforce: null, type: ObjectType.Other);

	private MissileFlightEffect[] _flightEffects;

	private float _flightStartTime = -1f;

	private bool _stopBoosterAudioFirst;

	public bool IsMotorBurning
	{
		get
		{
			if (_isBoosterEffectStarted)
			{
				return CalculateThrustOverTime(_ap, _airLaunched, GameTime.time - _boosterEffectStartTime, GameTime.fixedDeltaTime) > 0f;
			}
			return false;
		}
	}

	public float _maxPitch { get; set; }

	public bool GroupLeader
	{
		get
		{
			if (_inMissileGroup)
			{
				return _missileGroup._leader == this;
			}
			return false;
		}
	}

	public ReactiveProperty<bool> GroupWingman { get; } = new ReactiveProperty<bool>
	{
		Value = false
	};

	private State state { get; set; }

	public Missile()
	{
		base.OnFlightStageChanged += SetMaxPitchAngle;
	}

	private void changeState(State state)
	{
		this.state = state;
	}

	public void ChangeToDebrisState()
	{
		changeState(new Debris(this));
	}

	public void ChangeToTerminalState()
	{
		changeState(new TerminalApproach(this));
	}

	public void ChangeDebrisToTerminatedState()
	{
		if (_debrisFireEffect != null)
		{
			ParticleSystem[] componentsInChildren = _debrisFireEffect.GetComponentsInChildren<ParticleSystem>();
			foreach (ParticleSystem particleSystem in componentsInChildren)
			{
				if (particleSystem.name.ToLower() == "fireemitter")
				{
					particleSystem.Stop();
				}
			}
			if (_debrisFireEffect.transform.parent != null && _debrisFireEffect.transform.parent.tag != "PoolManager")
			{
				_debrisFireEffect.transform.SetParent(null);
			}
		}
		ParticleAutoDestroy particleAutoDestroy = ParticleUtils.SetAutoDestroyAndCustomWorldSpace(_debrisFireEffect);
		if (particleAutoDestroy != null)
		{
			particleAutoDestroy.setTimer(0.5f);
		}
		Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._hitAirExplosionEffect, _weaponInstance._debrisMesh.transform.position, triggerCameraShake: true, _ap._warheadPower / 100f));
		changeState(new Terminated(this));
	}

	public override void OnStart()
	{
		base.OnStart();
		changeState(new Inactive(this));
	}

	public override void Destruction(Vector3 position, Quaternion rotation, ObjectBase targetObject, bool createBlastzone = false)
	{
		DestroyTestInterceptPoint();
		base.Destruction(position, rotation, targetObject, createBlastzone);
	}

	public override void destroyObject(bool keepInFlightEffectAlive = false, bool score = false, TacView.TCEvent eventType = TacView.TCEvent.Destroyed)
	{
		if (_flightEffects != null)
		{
			MissileFlightEffect[] flightEffects = _flightEffects;
			foreach (MissileFlightEffect missileFlightEffect in flightEffects)
			{
				if (missileFlightEffect.type == MissileEffectType.Sustainer && !(missileFlightEffect.instance == null))
				{
					if (missileFlightEffect.instance.transform.parent != null && !missileFlightEffect.instance.transform.parent.CompareTag("PoolManager"))
					{
						missileFlightEffect.instance.transform.SetParent(null);
					}
					AudioSource component = missileFlightEffect.instance.GetComponent<AudioSource>();
					if (component != null)
					{
						component.Stop();
					}
					missileFlightEffect.instance.GetComponent<ParticleSystem>()?.Stop();
				}
			}
		}
		_flightEffects = null;
		_flightStartTime = -1f;
		base.destroyObject(keepInFlightEffectAlive, score, eventType);
	}

	public override void Container_Launch(ObjectBase targetObject, Vector3 bearingOnlyTargetPosition, Vector3 offsetVector, WeaponSystem weaponSystem)
	{
		if (base.gameObject == null)
		{
			return;
		}
		base.gameObject.SetActive(value: true);
		base.Container_Launch(targetObject, bearingOnlyTargetPosition, offsetVector, weaponSystem);
		CommonLaunchSettings(targetObject, bearingOnlyTargetPosition, weaponSystem);
		_wasInMissileGroup = false;
		_inMissileGroup = false;
		if (_launchPlatform != null && _launchPlatform.IsAirUnit)
		{
			changeState(new DropFromAircraft(this));
		}
		else
		{
			changeState(new Launch(this));
		}
		if ((base.CurrentIntendedTargetObject != null && base.CurrentIntendedTargetObject is Vessel) || base.CurrentIntendedTargetObject is Submarine || base.CurrentIntendedTargetObject is LandUnit)
		{
			_ap._ignoreHeightDifferenceForTargetDist = true;
		}
		if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.WireGuided)
		{
			_ap._terrainFollowFlight = true;
		}
		_requiresExternalGuidance = false;
		_seeker.init(_launchPlatform, this, _ap);
		if (_ap._requiresGuidance)
		{
			_requiresExternalGuidance = true;
			if (!CheckForJoinMissileGroup())
			{
				activateGuidingSystem();
			}
			if (_ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && _guidingRadarSystem != null)
			{
				_seeker.setGuidingSystem(_guidingRadarSystem);
			}
			else if (_ap._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
			{
				base.CurrentIntendedTargetObject = targetObject;
				_seeker.setCurrentTarget(targetObject);
				_seeker.setGuidingSystem();
			}
			string text = "to bearing";
			if (targetObject != null)
			{
				text = "at " + targetObject.getName();
			}
			if (_ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
			{
				Log.Debug("The SARH missile " + _ap._displayedName + " launched " + text, Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Missile.cs", 2555, "Container_Launch");
			}
			else if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				Log.Debug("The Radio Command missile " + _ap._displayedName + " launched " + text, Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Missile.cs", 2556, "Container_Launch");
			}
			else if (_ap._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
			{
				Log.Debug("The Laser guided missile " + _ap._displayedName + " launched " + text, Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Missile.cs", 2557, "Container_Launch");
			}
		}
		CalculateDeviation(checkSeekerSwap: false);
		if (_ap._guidanceType == AmmunitionParameters.GuidanceType.IRHoming && _initialSetTargetObject != null && !_seeker.IsOn.Value && _ap._terminalApproachDist > (base.transform.position - _initialSetTargetObject.transform.position).magnitude)
		{
			_seeker.setActive(active: true);
		}
		_lostGuidanceTime = GameTime.time;
		if (_ap._loftAltMode == AmmunitionParameters.LoftMode.Launcher && _launchPlatform != null)
		{
			_ap._maxLoftAltUnity = _launchPlatform.transform.position.y;
		}
		if (_ap._loftAltMode == AmmunitionParameters.LoftMode.Target)
		{
			if (base.CurrentIntendedTargetObject != null)
			{
				_ap._maxLoftAltUnity = base.CurrentIntendedTargetObject.transform.position.y;
			}
			else if (_launchPlatform != null)
			{
				_ap._maxLoftAltUnity = _launchPlatform.transform.position.y;
			}
		}
		_testObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		_testObject.name = "INTERCEPT " + getUIDAndName();
		_testObject.GetComponent<SphereCollider>().enabled = false;
		_testObject.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
		_testObjectMeshRenderer = _testObject.GetComponent<MeshRenderer>();
		if (_testObjectMeshRenderer != null)
		{
			_testObjectMeshRenderer.enabled = false;
		}
		_waypointPathRenderer = _testObject.AddComponent<TrailRenderer>();
		_waypointPathRenderer.startWidth = 0.05f;
		_waypointPathRenderer.endWidth = 0.01f;
		_waypointPathRenderer.time = 300f;
		_waypointPathRenderer.material = new Material(Shader.Find("Sprites/Default"));
		_waypointPathRenderer.startColor = Color.cyan;
		_waypointPathRenderer.endColor = Color.magenta;
		_waypointPathRenderer.enabled = false;
		_waypointPathRenderer.Clear();
	}

	public void CalculateDeviation(bool checkSeekerSwap = true)
	{
		if (checkSeekerSwap)
		{
			CheckSeekerSwap();
		}
		float x = (float)(Globals._rnd.NextDouble() - 0.5) * 2f;
		float y = (float)(Globals._rnd.NextDouble() - 0.5) * 2f;
		float z = (float)(Globals._rnd.NextDouble() - 0.5) * 2f;
		_deviationVector = new Vector3(x, y, z).normalized;
		float num = _ap._circularErrorRadius;
		ObjectBase currentTarget = base.CurrentTarget;
		if (currentTarget != null)
		{
			if (currentTarget is Vessel || currentTarget is Submarine)
			{
				if (_ap._targetType == Ammunition.Target.AAW || _ap._secondaryTargetType == Ammunition.Target.AAW)
				{
					num = 0f;
				}
			}
			else if (currentTarget is Aircraft aircraft)
			{
				if (aircraft.Ap._isLarge)
				{
					num = _ap._circularErrorRadiusLarge;
				}
			}
			else if (currentTarget is LandUnit landUnit)
			{
				if (landUnit._isMobile)
				{
					num = _ap._circularErrorRadiusMobileUnit;
				}
				else
				{
					num = _ap._circularErrorRadiusInstallation;
					if (base._taskforce._spottedObjects.Contains(landUnit))
					{
						num /= 10f;
					}
				}
			}
			if (currentTarget.isAirTarget())
			{
				_isTargetAirborne = true;
			}
			else
			{
				_isTargetAirborne = false;
			}
			_currentIntendedTargetData = (taskforce: currentTarget._taskforce, type: currentTarget._type);
		}
		else
		{
			_isTargetAirborne = false;
			_currentIntendedTargetData = (taskforce: null, type: ObjectType.Other);
		}
		_CEPdeviationMagnitude = 0f;
		if (currentTarget != null && (currentTarget.IsAirUnit || currentTarget.isWeapon()))
		{
			num = -1f;
			if (currentTarget.isWeapon())
			{
				_CEPdeviationMagnitude = Utils.RandomFloat(2f, 4f) * 0.0148809375f;
			}
			else
			{
				_CEPdeviationMagnitude = Utils.RandomFloat(2f, 6f) * 0.0148809375f;
			}
			if (_CEPdeviationMagnitude > _ap._fuzeProximityDistance)
			{
				_CEPdeviationMagnitude = _ap._fuzeProximityDistance - 0.0001f;
			}
		}
		if (num > 0.0001f)
		{
			_CEPdeviationMagnitude = Mathf.Abs(Utils.sampleGauss(Globals._rnd) * 3f * num / 0.675f);
			if (_CEPdeviationMagnitude > num * 3f)
			{
				_CEPdeviationMagnitude = Utils.RandomFloat(num * 2f, num * 3f);
			}
		}
		_TVRCdeviationMagnitude = 1f - _ap._seekerPassiveRange / 303.15445f;
		_TVRCdeviationMagnitude *= Mathf.Abs(Utils.sampleGauss(Globals._rnd) * 3f * 2f * 27.559496f / 0.675f);
		if (_TVRCdeviationMagnitude > 55.11899f)
		{
			_TVRCdeviationMagnitude = Utils.RandomFloat(1.5f, 2.5f) * 27.559496f;
		}
		if (base.CurrentIntendedTargetObject != null && base.CurrentIntendedTargetObject.IsAirUnit)
		{
			_CEPdeviationMagnitude /= 2f;
		}
		if (_isTargetAirborne && base._taskforce == Globals._playerTaskforce && _CEPdeviationMagnitude > _ap._fuzeProximityDistance && Globals._rnd.Next(0, 100) < Globals._missileCEPOverrideChance)
		{
			_CEPdeviationMagnitude = Utils.RandomFloat(_ap._fuzeProximityDistance / 2f, _ap._fuzeProximityDistance);
		}
		_finalDeviationMagnitude = _CEPdeviationMagnitude;
		_deviationMagnitudeWithAltitudeDebuff = Mathf.Abs(Utils.sampleGauss(Globals._rnd) * 3f * (num * _ap._minAltMalusFactor) / 0.675f);
	}

	public override void OnUpdateEveryFrame()
	{
		if (GameTime.IsPaused() || !_isLaunched)
		{
			return;
		}
		base.OnUpdateEveryFrame();
		if (base.transform.position.y < 0f && !_checkForWaterExit && base._flightStage != FlightStage.Launch && base._flightStage != FlightStage.UnderwaterLaunch && base._flightStage != FlightStage.UnderwaterMovement)
		{
			Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(_ap._hitWaterSplashEffect, null, base.transform.position));
			DM.Log(FullIDData() + " is destroyed by collision with water", DM.EventLogFilters.Destruction);
			Destruction(base.transform.position, base.transform.rotation, null);
		}
		GroupWingman.Value = _inMissileGroup && _missileGroup._leader != this;
		if (base.IsDestroyed || GroupWingman.Value || base._flightStage == FlightStage.Launch || base._flightStage == FlightStage.DropFromAircraft || base._flightStage == FlightStage.ToBearing || ((_wasInMissileGroup || !_requiresExternalGuidance || _seeker._guidanceType != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming) && _ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand))
		{
			return;
		}
		if (_guidingRadarSystem == null)
		{
			base.ConnectionLostForever.Value = true;
		}
		if (base.ConnectionLostForever.Value)
		{
			return;
		}
		base.ConnectionLost.Value = false;
		if (!_guidingRadarSystem.IsOn.Value)
		{
			base.ConnectionLost.Value = true;
		}
		else if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && !isValidTarget(base.CurrentIntendedTargetObject))
		{
			base.ConnectionLost.Value = true;
		}
		else
		{
			CheckRadioConnection();
		}
		if (_guidingRadarSystem._type == SensorSystemRadar.RadarType.DirectedSearch && base.CurrentIntendedTargetObject != null)
		{
			if (_guidingRadarSystem._mountObject == null)
			{
				base.ConnectionLost.Value = true;
			}
			else if (!_guidingRadarSystem.IsObjectInFOV(_guidingRadarSystem._mountObject.transform.position, _guidingRadarSystem._mountObject.transform.forward, base.CurrentIntendedTargetObject.transform.position))
			{
				base.ConnectionLost.Value = true;
			}
			else
			{
				base.ConnectionLost.Value = false;
			}
		}
		if (base.ConnectionLost.Value && NoSeekerActive())
		{
			if (_seeker._guidanceType != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && GameTime.time - _lostGuidanceTime > 10f)
			{
				_guidingRadarSystem.FreeChannels(base.CurrentIntendedTargetObject, this);
				base.ConnectionLostForever.Value = true;
			}
		}
		else if (!base.ConnectionLost.Value)
		{
			_lostGuidanceTime = GameTime.time;
			if (_guidingRadarSystem != null)
			{
				_guidingRadarSystem._isGuiding = true;
			}
		}
	}

	public override void OnFixedUpdate()
	{
		if (!_isLaunched)
		{
			return;
		}
		if (_seeker.isActive())
		{
			_seeker.update();
		}
		UpdateTargetData();
		_velocityInUnity = _velocityInKnots * 0.0076554087f;
		Vector3 direction = Vector3.forward * _velocityInUnity;
		_velocityVecInUnity = base.transform.TransformDirection(direction);
		_moveOfLaunchPlatform = Vector3.zero;
		_prevPitch = _currentPitch;
		_currentPitch = Utils.WrapAngle(base.transform.localEulerAngles.x);
		if (_externalDestructionNotified)
		{
			Log.Debug(getUIDAndName() + ": Got a notification of external destruction (" + base.transform.gameObject.name + ")", Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Missile.cs", 2800, "OnFixedUpdate");
			DM.Log(FullIDData() + " is destroyed by external means", DM.EventLogFilters.Destruction);
			Destruction(base.transform.position, base.transform.rotation, null);
			_externalDestructionNotified = false;
		}
		if (GameTime.time - _launchTime > _ap._maxFlightTime)
		{
			DM.Log($"{FullIDData()} was destroyed after exceeding its maximum lifetime. {GameTime.time - _launchTime}/{_ap._maxFlightTime}", DM.EventLogFilters.Destruction);
			Destruction(base.transform.position, base.transform.rotation, null);
		}
		_maintainingHeight = false;
		if (base.CurrentIntendedTargetObject != null)
		{
			if (_testObjectMeshRenderer != null)
			{
				if (_testObjectMeshRenderer.enabled && !DM._showInterceptPoint)
				{
					_testObjectMeshRenderer.enabled = false;
				}
				else if (!_testObjectMeshRenderer.enabled && DM._showInterceptPoint)
				{
					_testObjectMeshRenderer.enabled = true;
				}
			}
			if ((bool)_testObject)
			{
				_testObject.transform.position = (base.CommandPosition ?? base.AimPointGeoPosition).ToUnity();
			}
			if (_waypointPathRenderer != null)
			{
				if (_waypointPathRenderer.enabled && !DM._showInterceptPoint)
				{
					_waypointPathRenderer.enabled = false;
				}
				else if (!_waypointPathRenderer.enabled && DM._showInterceptPoint)
				{
					_waypointPathRenderer.enabled = true;
				}
			}
		}
		state.takeAction(this);
		if (state.GetType() == typeof(MaintainHeight) || state.GetType() == typeof(ToBearing) || state.GetType() == typeof(TerminalApproach) || state.GetType() == typeof(PerformSnakeSearch) || state.GetType() == typeof(EnterSearchMode))
		{
			(GeoPosition, float) tuple = CalculateInterceptAimPoint();
			(base.AimPointGeoPosition, _) = tuple;
			if (!_hasPoppedUp)
			{
				base.DesiredAltitude.Value = tuple.Item2;
			}
		}
		if (base._flightStage == FlightStage.DropFromAircraft)
		{
			PerformDropping();
			base.transform.position = _newPos;
		}
		else if (base._flightStage != FlightStage.Debris)
		{
			PerformMoveForward(_moveOfLaunchPlatform);
			CheckCollision();
			base.transform.position = _newPos;
			Vector3 vector = _velocityVecInUnity * GameTime.fixedDeltaTime;
			if (!base.CurrentTarget || !base.CurrentTarget.isAirTarget())
			{
				vector.y = 0f;
			}
			_traveledDistance += vector.magnitude;
			_remainingRangePercent = 101f - 100f * _traveledDistance / _ap._launchRangesInUnity.y;
			if (_remainingRangePercent < 0f && _ap.Kinematics == AmmunitionParameters.KinematicsLevel.None)
			{
				_remainingRangePercent = 0f;
				DM.I(getUIDAndName() + ": Self-destruct due to fuel empty");
				DM.Log(FullIDData() + " is destroyed by max distance", DM.EventLogFilters.Destruction);
				Destruction(base.transform.position, base.transform.rotation, null);
			}
			_lastVelocityVector = base.transform.forward * (_velocityInUnity * GameTime.fixedDeltaTime);
		}
		if (!base.IsDestroyed && _ap._maxGroupSize > 1 && !_wasInMissileGroup && !_inMissileGroup)
		{
			JoinOrCreateMissileGroup();
		}
		base.OnFixedUpdate();
	}

	private static bool IsStalled(AmmunitionParameters ap, float velocityInKnots, float thrust)
	{
		if (thrust > 0f || ap.Kinematics == AmmunitionParameters.KinematicsLevel.None)
		{
			return false;
		}
		return velocityInKnots < ap.MinVelocity;
	}

	public static List<WaypointConfig> CreateWaypointConfigs(WaypointCreationContext context)
	{
		AmmunitionParameters ap = context.Ap;
		List<WaypointConfig> list = new List<WaypointConfig>();
		float distanceToTarget = 0f;
		Waypoint.HeightType heightType = Waypoint.HeightType.AboveSeaLevel;
		Waypoint.HeightType heightType2 = Waypoint.HeightType.Absolute;
		if (context.TerrainFollow)
		{
			heightType = Waypoint.HeightType.AboveTerrain;
			heightType2 = Waypoint.HeightType.AboveTerrain;
		}
		Waypoint.HeightType heightType3 = ((context.IsAirTarget || ap._targetType == Ammunition.Target.AAW) ? Waypoint.HeightType.RelativeToTarget : heightType2);
		if (ap._terminalDiveDistance > 0f && ap._terminalDiveDistance < ap._terminalApproachDist && !context.IsAirTarget && !ap._terminalLoft)
		{
			list.Add(new WaypointConfig
			{
				DistanceToTarget = 0f,
				Settings = new Waypoint.Settings
				{
					PitchMode = Waypoint.ToHeightType.Linear,
					TargetSpeedKnots = ((ap._terminalVelocityInKnots > 0f) ? ap._terminalVelocityInKnots : ap._maxVelocityInKnots),
					EndHeight = new Waypoint.HeightSettings
					{
						HeightType = Waypoint.HeightType.RelativeToTarget,
						Altitude = 0f
					},
					HeightLimits = new Waypoint.AltitudeLimits
					{
						MinHeight = new Waypoint.HeightSettings[1]
						{
							new Waypoint.HeightSettings
							{
								HeightType = Waypoint.HeightType.Absolute,
								Altitude = 0.001f
							}
						}
					}
				}
			});
			distanceToTarget = ap._terminalDiveDistance;
		}
		if (ap._terminalPopUpDist > 0f && !context.SelectivePopUpDisabled)
		{
			list.RemoveAt(0);
			list.Add(new WaypointConfig
			{
				DistanceToTarget = 0f,
				Settings = new Waypoint.Settings
				{
					PitchMode = Waypoint.ToHeightType.KappaLoft,
					TargetSpeedKnots = ((ap._terminalVelocityInKnots > 0f) ? ap._terminalVelocityInKnots : ap._maxVelocityInKnots),
					AllowExceedingAngleLimits = (ap.Kinematics != AmmunitionParameters.KinematicsLevel.None),
					Constraints = new Waypoint.HeightConstraints
					{
						ClimbAngle = ap._terminalPopUpAngle,
						DescentAngle = ap._terminalPopUpAngle
					},
					HeightLimits = new Waypoint.AltitudeLimits
					{
						MinHeight = new Waypoint.HeightSettings[1]
						{
							new Waypoint.HeightSettings
							{
								HeightType = heightType,
								Altitude = 0f
							}
						}
					},
					LoftHeight = new Waypoint.HeightSettings
					{
						HeightType = Waypoint.HeightType.RelativeToTarget,
						Altitude = ap._terminalPopUpHeight
					},
					EndHeight = new Waypoint.HeightSettings
					{
						HeightType = Waypoint.HeightType.RelativeToTarget,
						Altitude = 0f
					}
				}
			});
			distanceToTarget = ap._terminalPopUpDist;
		}
		if (ap._terminalApproachDist > 0f && !ap._terminalLoft)
		{
			float num = (context.IsAirTarget ? 0f : ap._terminalAltUnity);
			if (num < 0f && ap.Kinematics == AmmunitionParameters.KinematicsLevel.None && list.Count != 0)
			{
				num = ((ap._finalFlightPhaseDistToTarget > 0f) ? ap._finalFlightPhaseAltUnity : ap._seaSkimmingAltUnity);
			}
			Waypoint.HeightType heightType4 = heightType3;
			if (heightType4 == Waypoint.HeightType.Absolute && num < 0f)
			{
				heightType4 = Waypoint.HeightType.OnLineToTarget;
			}
			if (context.IsGroupLeader)
			{
				num = context.Ap._groupLeaderAltUnity;
			}
			list.Add(new WaypointConfig
			{
				DistanceToTarget = distanceToTarget,
				Settings = new Waypoint.Settings
				{
					PitchMode = ((ap.Kinematics != AmmunitionParameters.KinematicsLevel.None || context.IsAirTarget || heightType4 == Waypoint.HeightType.OnLineToTarget) ? Waypoint.ToHeightType.Linear : Waypoint.ToHeightType.Immediate),
					TargetSpeedKnots = ((ap._terminalVelocityInKnots > 0f) ? ap._terminalVelocityInKnots : ap._maxVelocityInKnots),
					HeightLimits = new Waypoint.AltitudeLimits
					{
						MinHeight = new Waypoint.HeightSettings[1]
						{
							new Waypoint.HeightSettings
							{
								HeightType = heightType,
								Altitude = 0f
							}
						}
					},
					Constraints = new Waypoint.HeightConstraints
					{
						ClimbAngle = ap._finalFlightPhaseMaxAngle,
						DescentAngle = ap._finalFlightPhaseMaxAngle
					},
					EndHeight = new Waypoint.HeightSettings
					{
						HeightType = heightType4,
						Altitude = num
					}
				}
			});
			distanceToTarget = (ap._terminalLoft ? 0.5f : ap._terminalApproachDist);
		}
		if (ap._finalFlightPhaseDistToTargetUnity > 0f)
		{
			list.Add(new WaypointConfig
			{
				DistanceToTarget = distanceToTarget,
				Settings = new Waypoint.Settings
				{
					PitchMode = Waypoint.ToHeightType.Immediate,
					TargetSpeedKnots = ap._maxVelocityInKnots,
					AllowExceedingAngleLimits = (ap.Kinematics != AmmunitionParameters.KinematicsLevel.None),
					HeightLimits = new Waypoint.AltitudeLimits
					{
						MinHeight = new Waypoint.HeightSettings[1]
						{
							new Waypoint.HeightSettings
							{
								HeightType = heightType,
								Altitude = 0f
							}
						}
					},
					Constraints = new Waypoint.HeightConstraints
					{
						ClimbAngle = ap._finalFlightPhaseMaxAngle,
						DescentAngle = ap._finalFlightPhaseMaxAngle
					},
					EndHeight = new Waypoint.HeightSettings
					{
						HeightType = heightType2,
						Altitude = (context.IsGroupLeader ? ap._groupLeaderAltUnity : ap._finalFlightPhaseAltUnity)
					}
				}
			});
			distanceToTarget = ap._finalFlightPhaseDistToTargetUnity;
		}
		if (ap._seaSkimmingStartDistToTargetUnity > 0f)
		{
			list.Add(new WaypointConfig
			{
				DistanceToTarget = distanceToTarget,
				Settings = new Waypoint.Settings
				{
					PitchMode = Waypoint.ToHeightType.Immediate,
					TargetSpeedKnots = ap._maxVelocityInKnots,
					AllowExceedingAngleLimits = (ap.Kinematics != AmmunitionParameters.KinematicsLevel.None),
					HeightLimits = new Waypoint.AltitudeLimits
					{
						MinHeight = new Waypoint.HeightSettings[1]
						{
							new Waypoint.HeightSettings
							{
								HeightType = heightType,
								Altitude = 0f
							}
						}
					},
					Constraints = new Waypoint.HeightConstraints
					{
						ClimbAngle = ap._seaSkimmingMaxDescentAngle,
						DescentAngle = ap._seaSkimmingMaxDescentAngle
					},
					EndHeight = new Waypoint.HeightSettings
					{
						HeightType = heightType3,
						Altitude = ap._seaSkimmingAltUnity
					}
				}
			});
			distanceToTarget = ap._seaSkimmingStartDistToTargetUnity;
		}
		float num2 = Mathf.Max((context.LoftAltUnityOverride >= 0f) ? context.LoftAltUnityOverride : ap._maxLoftAltUnity, context.LaunchAltUnity);
		if (ap._maxLoftAltUnity > 0f)
		{
			Waypoint.HeightSettings heightSettings = new Waypoint.HeightSettings
			{
				HeightType = Waypoint.HeightType.RelativeToTarget,
				Altitude = (context.IsAirTarget ? 0f : (ap._maxLoftAltUnity / 2f))
			};
			List<Waypoint.HeightSettings> list2 = new List<Waypoint.HeightSettings>();
			bool flag = ap.Kinematics == AmmunitionParameters.KinematicsLevel.None && !context.IsAirTarget;
			if (flag)
			{
				if (ap._terrainFollowFlight)
				{
					list2.Add(new Waypoint.HeightSettings
					{
						HeightType = Waypoint.HeightType.AboveTerrain,
						Altitude = ap._maxLoftAltUnity
					});
				}
				else
				{
					list2.Add(new Waypoint.HeightSettings
					{
						HeightType = Waypoint.HeightType.AboveSeaLevel,
						Altitude = ap._maxLoftAltUnity
					});
				}
			}
			list.Add(new WaypointConfig
			{
				DistanceToTarget = distanceToTarget,
				Settings = new Waypoint.Settings
				{
					PitchMode = ((!flag) ? Waypoint.ToHeightType.KappaLoft : Waypoint.ToHeightType.Immediate),
					TargetSpeedKnots = ap._maxLoftVelocityInKnots,
					SkipNextWaypointOnComplete = !ap._loftToSkim,
					AllowExceedingAngleLimits = (ap.Kinematics != AmmunitionParameters.KinematicsLevel.None || context.IsAirTarget),
					HeightLimits = new Waypoint.AltitudeLimits
					{
						MinHeight = new Waypoint.HeightSettings[1]
						{
							new Waypoint.HeightSettings
							{
								HeightType = heightType,
								Altitude = 0f
							}
						}
					},
					EndHeight = new Waypoint.HeightSettings
					{
						HeightType = ((!flag) ? Waypoint.HeightType.OnLineToTarget : Waypoint.HeightType.Absolute),
						Altitude = ((!flag) ? 0f : (ap._maxLoftAltUnity / 2f))
					},
					LoftHeight = new Waypoint.HeightSettings
					{
						HeightType = Waypoint.HeightType.Absolute,
						Altitude = (flag ? ap._maxLoftAltUnity : num2)
					},
					LoftHeightLimits = new Waypoint.AltitudeLimits
					{
						MinHeight = list2.ToArray()
					},
					Constraints = new Waypoint.HeightConstraints
					{
						ClimbAngle = ap._maxLoftAngle,
						DescentAngle = ap._finalFlightPhaseMaxAngle,
						ClimbAggressiveness = 0.8f,
						StartAngle = context.StartAngle
					}
				}
			});
		}
		list.Reverse();
		return list;
	}

	private static SimulationWaypointState CreateSimulationWaypoints(AmmunitionParameters ap, Vector3 launchPosition, Vector3 targetPosition, float startPitch, float loftAltOverride = -1f)
	{
		List<WaypointConfig> list = CreateWaypointConfigs(new WaypointCreationContext
		{
			Ap = ap,
			IsAirTarget = (ap._targetType == Ammunition.Target.AAW),
			TerrainFollow = ap._terrainFollowFlight,
			IsGroupLeader = false,
			StartAngle = startPitch * 57.29578f,
			SelectivePopUpDisabled = false,
			LaunchAltUnity = launchPosition.y,
			LoftAltUnityOverride = loftAltOverride
		});
		List<Waypoint> list2 = new List<Waypoint>();
		Waypoint.Context context = new Waypoint.Context
		{
			GeoPosition = new GeoPosition(launchPosition),
			UnityPosition = launchPosition,
			VelocityVector = Vector3.zero,
			FlatVelocity = 0f
		};
		foreach (WaypointConfig item2 in list)
		{
			Waypoint item = new Waypoint(new Waypoint.DistanceToTargetWaypoint(item2.DistanceToTarget), item2.Settings, context);
			list2.Add(item);
		}
		return new SimulationWaypointState
		{
			Waypoints = list2,
			LastWaypointPosition = new GeoPosition(launchPosition)
		};
	}

	private static void UpdateSimulationWaypointContexts(SimulationWaypointState waypointState, Vector3 currentPos, Vector3 velocity, float stallSpeedKnots = 0f, bool motorBurning = false, float targetVerticalVelocity = 0f)
	{
		Waypoint.Context context = new Waypoint.Context
		{
			GeoPosition = new GeoPosition(currentPos),
			UnityPosition = currentPos,
			VelocityVector = velocity,
			FlatVelocity = velocity.magnitude,
			StallSpeedKnots = stallSpeedKnots,
			MotorBurning = motorBurning
		};
		foreach (Waypoint waypoint in waypointState.Waypoints)
		{
			waypoint.UpdateContext(context);
		}
	}

	public static KinematicRangeResult SimulateShotLinear(AmmunitionParameters ap, Vector3 launchPosition, float startVelocityKnots, Vector3 targetVelocityVector, Vector3 targetPosition, bool isAirUnit, float dragFactorOverride = -1f, float stepsPerMile = -1f, float timeoutOverride = -1f, List<Vector3> trajectoryOut = null, List<float> speedsOut = null, List<float> timesOut = null, float loftAltOverride = -1f, bool targetEvasiveManuver = true)
	{
		if (trajectoryOut == null)
		{
			trajectoryOut = new List<Vector3>();
		}
		if (targetVelocityVector.magnitude > 0f && targetEvasiveManuver)
		{
			targetVelocityVector += (targetPosition - launchPosition).normalized * (targetVelocityVector.magnitude * 0.25f);
		}
		float num = launchPosition.FlatDistance(targetPosition);
		if (num < 0.001f)
		{
			return new KinematicRangeResult
			{
				MaxRange = 1f,
				DistPercent = 1f,
				InterceptTime = 0f,
				InterceptSpeed = startVelocityKnots
			};
		}
		if (dragFactorOverride < -0.1f)
		{
			dragFactorOverride = ap.GetDragFactor(isAirUnit);
		}
		float num2 = ((ap._maxFlightTime > 0f) ? ap._maxFlightTime : 600f);
		if (timeoutOverride > 0f)
		{
			num2 = timeoutOverride;
		}
		(float, float) accelerationTimes = GetAccelerationTimes(ap, isAirUnit);
		float num3 = CalculateMissileInterceptTimeAnalytical(ap, launchPosition, startVelocityKnots, targetPosition, targetVelocityVector, dragFactorOverride).interceptTime;
		if (num3 < 0f)
		{
			num3 = CalculateMissileInterceptTimeSimple(ap, launchPosition, targetPosition, targetVelocityVector);
		}
		SimulationWaypointState simulationWaypointState = CreateSimulationWaypoints(ap, launchPosition, targetPosition + targetVelocityVector * num3, ap._maxLoftAngle * (MathF.PI / 180f), loftAltOverride);
		Vector3 vector = launchPosition;
		float num4 = startVelocityKnots;
		float num5 = float.MaxValue;
		Vector3 vector2 = new Vector3(targetPosition.x - launchPosition.x, 0f, targetPosition.z - launchPosition.z);
		Vector3 vector3 = ((vector2.sqrMagnitude > 0.0001f) ? vector2.normalized : Vector3.forward);
		float num6 = float.MaxValue;
		Vector3 vector4 = launchPosition;
		float num7 = 0f;
		float num8 = 0f;
		float num9 = ap._deceleration * 9.8f * 1.94384f;
		bool loftTooHigh = false;
		Vector3 vector5 = targetPosition;
		float num10 = vector.FlatDistance(vector5);
		float num11 = vector5.y - vector.y;
		bool targetTooClose = false;
		float num12 = MathF.Sqrt(num10 * num10 + num11 * num11);
		Vector3 rhs = targetPosition - launchPosition;
		rhs.y = 0f;
		if (rhs.sqrMagnitude > 0.001f)
		{
			rhs.Normalize();
		}
		bool flag = Vector3.Dot(targetVelocityVector, rhs) < 0f;
		trajectoryOut?.Add(launchPosition);
		speedsOut?.Add(num4);
		timesOut?.Add(0f);
		for (float num13 = 0f; num13 < num2; num13 += 0.5f)
		{
			float num14 = num4 * 0.0076554087f;
			if (num10 < num6)
			{
				num6 = num10;
				vector4 = vector;
				num7 = num13;
			}
			if (num13 > num3 / 6f)
			{
				Vector3 lhs = vector5 - vector;
				float num15 = num14;
				if (num13 < ap.TotalBurnTime)
				{
					num15 = MathF.Max(num15, 0.0076554087f * (ap.TotalDeltaV / 2f + startVelocityKnots));
				}
				float num16 = num15 * num15 * 0.8f - targetVelocityVector.sqrMagnitude;
				float num17 = -2f * Vector3.Dot(lhs, targetVelocityVector);
				float num18 = 0f - lhs.sqrMagnitude;
				float num19 = num17 * num17 - 4f * num16 * num18;
				if (Mathf.Abs(num16) > 0.001f && num19 >= 0f)
				{
					float num20 = (0f - num17 + Mathf.Sqrt(num19)) / (2f * num16);
					if (num20 > 0f)
					{
						num3 = num13 + num20;
					}
					else if (num14 > 0.001f)
					{
						num3 = num13 + num12 / num14;
					}
				}
				else
				{
					float num21 = targetVelocityVector.sqrMagnitude * 0.8f;
					float num22 = ((num21 > 0.001f) ? Mathf.Max(0f, (0f - Vector3.Dot(lhs, targetVelocityVector)) / num21) : ((num14 > 0.001f) ? (num12 / num14) : 0f));
					num3 = num13 + num22;
				}
			}
			Vector3 vector6 = targetPosition + targetVelocityVector * num3;
			float num23 = Mathf.Clamp01(1f - Mathf.Pow(vector.FlatDistance(vector6) / Mathf.Max(ap._launchRangesInUnity.y, 1f), 2f));
			Vector3 position = Vector3.Lerp(vector6, vector5, 1f - num23);
			Vector3 velocity = vector3 * num14;
			bool flag2 = num13 < accelerationTimes.Item1 + Mathf.Max(0f, accelerationTimes.Item2);
			UpdateSimulationWaypointContexts(simulationWaypointState, vector, velocity, ap.MinVelocity, flag2, targetVelocityVector.y);
			Waypoint.UpdateAndGetActiveWaypoint(ref simulationWaypointState.Waypoints, ref simulationWaypointState.LastWaypointPosition, new GeoPosition(position), 0.5f);
			Vector3 vector7 = vector3 * num14;
			Vector3 relPos = vector5 - vector;
			Vector3 relVel = targetVelocityVector - vector7;
			Vector3 vector8 = ComputePN(vector7, relPos, relVel, ap._navigationGain);
			float num24 = ap._maxTurnRateDegrees * (MathF.PI / 180f) * num14;
			if (vector8.sqrMagnitude > num24 * num24)
			{
				vector8 = vector8.normalized * num24;
			}
			Vector3 vector9 = vector7 + vector8 * 0.5f;
			Vector3 vector10 = new Vector3(vector9.x, 0f, vector9.z);
			if (vector10.sqrMagnitude < 0.0001f)
			{
				vector10 = new Vector3(vector3.x, 0f, vector3.z);
			}
			if (vector10.sqrMagnitude < 0.0001f)
			{
				vector10 = new Vector3(relPos.x, 0f, relPos.z);
			}
			vector10.Normalize();
			Vector3 a = ((simulationWaypointState.Waypoints.Count > 0) ? simulationWaypointState.Waypoints[0].WpStatus.DesiredPosition.ToUnity() : vector6);
			float num25 = Mathf.Clamp(Mathf.Atan2(a.y - vector.y, a.FlatDistance(vector)), -1.5707961f, 1.5707961f);
			vector3 = new Vector3(vector10.x * Mathf.Cos(num25), Mathf.Sin(num25), vector10.z * Mathf.Cos(num25));
			float num26 = (0f - num25) * 57.29578f;
			if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.None)
			{
				float num27 = ((simulationWaypointState.Waypoints.Count > 0) ? simulationWaypointState.Waypoints[0].WpSettings.TargetSpeedKnots : 0f);
				float num28 = ((num27 > 0f) ? num27 : ap._maxVelocityInKnots);
				if (num4 < num28)
				{
					float num29 = CalculateThrustOverTime(ap, isAirUnit, num13, 0.5f);
					num4 = Mathf.Min(num4 + num29, num28);
				}
				else if (num4 > num28)
				{
					num4 = Mathf.Max(num4 - num9 * 0.5f, num28);
				}
			}
			else
			{
				float num30 = CalculateThrustOverTime(ap, isAirUnit, num13, 0.5f);
				float num31 = CalculateDrag(vector.y, num14, 0.5f, num26, dragFactorOverride, liftFactor: ap.LiftFactor, motorBurning: num13 < accelerationTimes.Item1 + MathF.Max(0f, accelerationTimes.Item2), targetAltitude: vector5.y, stallSpeedKnots: ap.MinVelocity);
				num4 = Mathf.Max(num4 + num30 - num31, 0f);
			}
			if (!flag2)
			{
				num5 = Mathf.Min(num5, num4);
			}
			float num32 = num4 * 0.0076554087f;
			if (num12 > 0.0001f)
			{
				Vector3 rhs2 = (vector5 - vector) / num12;
				if (num8 * 0.5f * 2f >= num12)
				{
					float effectiveVelKnots = ((num5 < float.MaxValue) ? Mathf.Min(num4, num5) : num4);
					float pitchDeg = ((num10 > 0.001f) ? (57.29578f * (0f - Mathf.Atan2(vector5.y - vector.y, num10))) : 0f);
					float num33 = CalculateRemainingRange(ap, vector.y, effectiveVelKnots, launchPosition, vector5, targetVelocityVector, num2 - num13, dragFactorOverride, pitchDeg);
					trajectoryOut?.Add(vector5);
					speedsOut?.Add(num4);
					timesOut?.Add(num13);
					return new KinematicRangeResult
					{
						MaxRange = num + num33,
						DistPercent = (num + num33) / num,
						InterceptTime = num13,
						InterceptSpeed = num4,
						TargetTooClose = targetTooClose
					};
				}
				num8 = Vector3.Dot(vector3 * num32 - targetVelocityVector, rhs2);
			}
			vector += vector3 * (num32 * 0.5f);
			trajectoryOut?.Add(vector);
			speedsOut?.Add(num4);
			timesOut?.Add(num13 + 0.5f);
			vector5 = targetPosition + targetVelocityVector * (num13 + 0.5f);
			num10 = vector.FlatDistance(vector5);
			num11 = vector5.y - vector.y;
			num12 = MathF.Sqrt(num10 * num10 + num11 * num11);
			if (flag && vector.FlatDistance(targetPosition) > num * 1.05f)
			{
				targetTooClose = true;
			}
			if (num13 > 1f && !flag2)
			{
				Utils.CalculateAirDensity(vector.y);
				if (num4 < ap.MinVelocity + 200f && simulationWaypointState.Waypoints.Any())
				{
					Waypoint waypoint = simulationWaypointState.Waypoints[0];
					if (waypoint != null)
					{
						Waypoint.Settings wpSettings = waypoint.WpSettings;
						if (wpSettings != null && wpSettings.LoftHeight.HasValue)
						{
							simulationWaypointState.Waypoints.FirstOrDefault().WpStatus.Done = true;
							loftTooHigh = true;
							goto IL_0916;
						}
					}
				}
				if (num4 < ap.MinVelocity)
				{
					break;
				}
			}
			goto IL_0916;
			IL_0916:
			if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.None && vector.FlatDistance(launchPosition) > ap._launchRangesInUnity.y)
			{
				break;
			}
		}
		if (num7 < 0.2f)
		{
			return new KinematicRangeResult
			{
				MaxRange = 0f,
				DistPercent = 0f,
				InterceptTime = -1f,
				InterceptSpeed = -1f,
				TargetTooClose = targetTooClose,
				LoftTooHigh = loftTooHigh
			};
		}
		Vector3 vector11 = vector4 - launchPosition;
		vector11.y = 0f;
		Vector3 b = targetPosition + targetVelocityVector * num7;
		float num34 = vector4.FlatDistance(b);
		float num35 = num - num34;
		b.y = 0f;
		float distPercent = num35 / MathF.Max(num, 1f);
		return new KinematicRangeResult
		{
			MaxRange = num35,
			DistPercent = distPercent,
			InterceptTime = num7,
			InterceptSpeed = -1f,
			TargetTooClose = targetTooClose,
			LoftTooHigh = loftTooHigh
		};
	}

	private static float CalculateRemainingRange(AmmunitionParameters ap, float altitudeUnity, float effectiveVelKnots, Vector3 launchPosition, Vector3 currentTargetPos, Vector3 targetVelocityVector, float remainingLifetime, float dragFactorOverride, float pitchDeg = 0f)
	{
		float num = effectiveVelKnots * 0.0076554087f;
		float num2 = CalculateDrag(altitudeUnity, num, 1f, pitchDeg, dragFactorOverride, motorBurning: false, currentTargetPos.y, ap.LiftFactor, ap.MinVelocity) * 0.8f;
		float num3 = num2 * 0.0076554087f;
		float num4 = Vector3.Dot(targetVelocityVector, (currentTargetPos - launchPosition).normalized);
		float num5 = num - num4;
		if (num5 <= 0f)
		{
			return 0f;
		}
		float a = ((num2 > 0.001f) ? ((effectiveVelKnots - ap.MinVelocity) / num2) : remainingLifetime);
		float b = ((num3 > 0.001f) ? (num5 / num3) : remainingLifetime);
		float num6 = Mathf.Min(Mathf.Min(a, b), remainingLifetime);
		if (num6 <= 0f)
		{
			return 0f;
		}
		return num5 * num6 - 0.5f * num3 * num6 * num6;
	}

	private static float CalculateThrustOverTime(AmmunitionParameters ap, bool isAirLaunched, float timeSinceLaunch, float timeWindow)
	{
		float num = -1f;
		float num2 = ap._acceleration * 9.81f * 1.94384f * 1.2f;
		(float, float) accelerationTimes = GetAccelerationTimes(ap, isAirLaunched);
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.None && ap._accelerationTime == 6f)
		{
			accelerationTimes.Item1 = 9999999f;
		}
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.Full)
		{
			num = ap._sustainerBurnAcceleration * 9.81f * 1.94384f * 1.2f;
		}
		float num3 = accelerationTimes.Item1 - timeSinceLaunch;
		float num4 = accelerationTimes.Item2 + accelerationTimes.Item1 - timeSinceLaunch;
		if (num3 < 1E-05f && num4 <= 0.0001f)
		{
			return 0f;
		}
		if (num3 < 0f && num4 > 0f)
		{
			if (num4 < timeWindow)
			{
				return num4 * timeWindow;
			}
			return timeWindow * num;
		}
		if (num3 < timeWindow)
		{
			float num5 = (timeWindow - num3) * num;
			float num6 = num2 * num3;
			if (num5 > 0f)
			{
				num6 += num5;
			}
			return num6;
		}
		return num2 * timeWindow;
	}

	public static (float primaryTime, float secondaryTIme) GetAccelerationTimes(AmmunitionParameters ap, bool isAirLaunched)
	{
		float item = -1f;
		float item2;
		if (ap.Kinematics == AmmunitionParameters.KinematicsLevel.Full)
		{
			item2 = ap._accelerationTime;
			item = ap._sustainerBurnTime;
		}
		else
		{
			float num = (isAirLaunched ? 1000f : 0f);
			item2 = (ap._maxVelocityInKnots - num) / (ap._acceleration * 9.81f * 1.94384f * 1.2f);
		}
		return (primaryTime: item2, secondaryTIme: item);
	}

	private (GeoPosition pos, float height) CalculateInterceptAimPoint()
	{
		if (_deviationMagnitudeWithJam > 0f)
		{
			Vector3 vector = base.transform.position + base.transform.forward * 100f;
			vector.y = -10f;
			setCourseTowardsPosition(vector, maintainLocalX: false);
			return (pos: new GeoPosition(vector), height: 0f);
		}
		if (!_leadTarget)
		{
			setCourseTowardsPosition(_currentKnownTargetPosition, maintainLocalX: false);
			return (pos: Utils.worldPositionFromUnityToLongLat(_currentKnownTargetPosition, Globals._currentCenterTile), height: _currentKnownTargetPosition.y);
		}
		Vector3 currentKnownTargetPosition = _currentKnownTargetPosition;
		Vector3 lastKnownTargetVelocity = _lastKnownTargetVelocity;
		Vector3 position = base.transform.position;
		Vector3 vector2 = currentKnownTargetPosition - position;
		float magnitude = vector2.magnitude;
		Vector3 normalized = vector2.normalized;
		Vector3 position2 = currentKnownTargetPosition;
		if (lastKnownTargetVelocity != Vector3.zero)
		{
			Vector3? vector3 = CalculateLeadInterceptPoint(currentKnownTargetPosition, lastKnownTargetVelocity, normalized, magnitude);
			if (!vector3.HasValue)
			{
				SetMissileCourse(currentKnownTargetPosition, currentKnownTargetPosition, Vector3.zero);
				return (pos: Utils.worldPositionFromUnityToLongLat(_currentKnownTargetPosition, Globals._currentCenterTile), height: _currentKnownTargetPosition.y);
			}
			float num = MathF.Pow(1f - Mathf.Clamp01(magnitude / (magnitude + _ap._launchRangesInUnity.y)), 2f);
			position2 = vector3.Value * num + currentKnownTargetPosition * (1f - num);
		}
		if (base.CurrentTarget != null && !(base.CurrentTarget is LandUnit))
		{
			position2 += _deviationVector * _finalDeviationMagnitude;
		}
		_targetInterceptPosition = new GeoPosition(position2);
		if (_dt > _ap._initialFlightPhaseDuration)
		{
			Waypoint.UpdateAndGetActiveWaypoint(ref Waypoints, ref _lastWaypointPosition, _targetInterceptPosition);
		}
		else
		{
			_lastWaypointPosition = base._geoPosition;
		}
		if (Waypoints.Count > 0 && !Waypoints[0].WpStatus.Done)
		{
			Waypoint waypoint = Waypoints[0];
			Vector3 vector4 = waypoint.WpStatus.DesiredPosition.ToUnity();
			Vector3 futurePosition = waypoint.WpStatus.FuturePosition.ToUnity();
			bool useKappaGuidance = waypoint.WpSettings.PitchMode == Waypoint.ToHeightType.KappaLoft;
			if (_inMissileGroup && state is MaintainHeight)
			{
				Vector3 vector5 = (GroupLeader ? vector4 : _inGroupPosition.ToUnity());
				vector5.y = vector4.y;
				if (!GroupLeader)
				{
					Vector3 right = _missileGroup._leader.transform.right;
					float num2 = Vector3.Dot(_velocityVecInUnity, right);
					float f = Vector3.Dot(vector5 - base.transform.position, right);
					float num3 = Mathf.Clamp(3f * num2, 0f - Mathf.Abs(f), Mathf.Abs(f));
					vector5 -= right * num3;
				}
				position2 = SetMissileCourse(vector4, futurePosition, vector5, useKappaGuidance);
				base.CommandPosition = new GeoPosition(position2);
				return (pos: new GeoPosition(position2), height: position2.y);
			}
			if ((bool)_testObject)
			{
				_testObject.transform.position = vector4;
			}
			SetMissileCourse(vector4, futurePosition, Vector3.zero, useKappaGuidance);
			base.CommandPosition = new GeoPosition(position2);
			return (pos: new GeoPosition(position2), height: vector4.y);
		}
		base.CommandPosition = new GeoPosition(position2);
		return (pos: new GeoPosition(position2), height: position2.y);
	}

	private Vector3 SetMissileCourse(Vector3 goalPosition, Vector3 futurePosition, Vector3 groupPos, bool useKappaGuidance = false)
	{
		Vector3 vector = ((groupPos == Vector3.zero) ? goalPosition : groupPos);
		if (this == null)
		{
			return vector;
		}
		Vector3 vector2 = futurePosition - goalPosition;
		if (groupPos != Vector3.zero)
		{
			futurePosition = groupPos + vector2;
		}
		if (!base.CurrentTarget || !base.CurrentTarget.isAirTarget())
		{
			float num = _ap._maxTurnRateDegrees;
			if (state is ToBearing && _ap._toBearingTurnRateDegrees > 0f)
			{
				num = _ap._toBearingTurnRateDegrees;
			}
			float num2 = futurePosition.y - base.transform.position.y;
			float y = _velocityVecInUnity.y;
			Vector3 normalized = (futurePosition - vector).normalized;
			Vector3 forward = base.transform.forward;
			float num3 = Mathf.Abs(Mathf.Asin(normalized.y) - Mathf.Asin(forward.y)) * 57.29578f;
			Vector3 vector3 = vector - base.transform.position;
			float num4 = Vector3.Angle(to: new Vector3(vector3.x, 0f, vector3.z).normalized, from: new Vector3(forward.x, 0f, forward.z).normalized);
			float num5 = Mathf.Sqrt(num4 * num4 + num3 * num3);
			float num6 = ((num5 > 0.001f) ? (num3 / num5) : 1f);
			float num7 = num * num6;
			float num8 = num2 / y;
			float x = num3 / num8;
			if (!useKappaGuidance && MathF.Abs(x) > num7 && (double)num8 > 0.1 && vector != futurePosition)
			{
				vector.y = base.transform.position.y;
			}
		}
		else if (base.CurrentTarget != null && _seeker != null && _seeker.isActive() && base.CurrentIntendedTargetObject != null)
		{
			float num9 = base.EffectiveFOV * 0.4f;
			if (num9 > 0f)
			{
				Vector3 normalized2 = (vector - base.transform.position).normalized;
				Vector3 normalized3 = (base.CurrentTarget.transform.position - base.transform.position).normalized;
				float num10 = Vector3.Angle(normalized2, normalized3);
				if (num10 > num9)
				{
					float t = (num10 - num9) / num10;
					Vector3 vector4 = Vector3.Slerp(normalized2, normalized3, t);
					vector = base.transform.position + vector4 * (vector - base.transform.position).magnitude;
				}
			}
		}
		_pitchAngle = setCourseTowardsPosition(vector, maintainLocalX: false, state is ToBearing);
		return vector;
	}

	private Vector3? CalculateLeadInterceptPoint(Vector3 targetPos, Vector3 targetVel, Vector3 travelDir, float distance)
	{
		float num = _velocityInUnity;
		if (_traveledDistance * 4f < distance)
		{
			num = MathF.Max(num, _ap._maxVelocityInKnots * 0.0076554087f / 1.3f);
		}
		float num2 = SolveInterceptQuadratic(num, travelDir * distance, targetVel);
		if (num2 <= 0f)
		{
			return null;
		}
		float num3 = DragCorrectedAvgSpeed(num, _velocityInKnots, num2, _ap, _ap.GetDragFactor(_airLaunched), base.transform.position.y, targetPos.y);
		num2 = SolveInterceptQuadratic(num3, travelDir * distance, targetVel);
		if (num2 <= 0f)
		{
			return null;
		}
		float num4 = base.transform.position.y - targetPos.y;
		if (num4 > 1f && _ap._finalFlightPhaseMaxAngle > 0.001f)
		{
			float f = _ap._finalFlightPhaseMaxAngle * (MathF.PI / 180f) * 0.5f;
			num2 += num4 * Mathf.Tan(f) / num3;
		}
		return targetPos + targetVel * num2;
	}

	private static float DragCorrectedAvgSpeed(float initialSpeedUnity, float initialSpeedKnots, float interceptTime, AmmunitionParameters ap, float dragFactor, float altitude, float targetAltitude)
	{
		float num = Mathf.Max(altitude, (ap._maxLoftAltUnity > 0f) ? ap._maxLoftAltUnity : altitude);
		float num2 = num - targetAltitude;
		float num3 = 0.5f;
		if (num2 > 1f && ap._finalFlightPhaseMaxAngle > 0.001f && interceptTime > 0.001f && ap._maxLoftAltUnity > 0f)
		{
			float num4 = num2 / Mathf.Tan(ap._finalFlightPhaseMaxAngle * (MathF.PI / 180f));
			float num5 = initialSpeedUnity * interceptTime;
			num3 = Mathf.Clamp01(num4 / num5);
		}
		float altitude2 = num - num3 * num2 * 0.5f;
		float liftFactor = ap.LiftFactor;
		float num6 = CalculateDrag(altitude2, initialSpeedUnity, 1f, 0f, dragFactor, motorBurning: false, targetAltitude, liftFactor, ap.MinVelocity);
		float num7 = Mathf.Max(initialSpeedKnots - num6 * interceptTime, 0f);
		return (initialSpeedUnity + num7 * 0.0076554087f) * 0.5f;
	}

	private void UpdateTargetData()
	{
		CalcEstimatedTargetPosition();
	}

	public void CalcEstimatedTargetPosition()
	{
		bool flag = base.CurrentTarget == null || _interceptionData.GetValueOrDefault(base.CurrentTarget, defaultValue: true);
		if ((bool)base.CurrentTarget && flag)
		{
			float num = GameTime.time - _lostGuidanceTime;
			float num2 = 0.05f;
			if ((_seeker == null || _seeker._guidanceType != AmmunitionParameters.GuidanceType.AntiRadiationHoming) && base.CurrentTarget is LandUnit { _isMobile: false })
			{
				_currentKnownTargetPosition = base.CurrentTarget._geoPosition.ToUnity() + _currentTargetPoint._position;
				return;
			}
			if (_seeker.isActive())
			{
				if (_seeker._lockedOnTarget != null)
				{
					_currentKnownTargetPosition = _seeker.getEstimatedTargetPosition();
					base.InitialTargetGeoPosition = base.CurrentTarget._geoPosition;
					_lastKnownTargetVelocity = base.CurrentTarget.UnityVelocityVector;
					return;
				}
				if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.AntiRadiationHoming)
				{
					_currentKnownTargetPosition = ((RadiationSeeker)_seeker)._lastKnownGeoPos.ToUnity();
					return;
				}
			}
			if (num < 0.0001f)
			{
				_lastKnownTargetVelocity = base.CurrentTarget.UnityVelocityVector;
				_currentKnownTargetPosition = base.CurrentTarget._geoPosition.ToUnity();
				if (!base.CurrentTarget.IsDestroyed)
				{
					_currentKnownTargetPosition.y = base.CurrentTarget.VisibleAltitude * num2 + _currentKnownTargetPosition.y * (1f - num2);
				}
				base.InitialTargetGeoPosition = base.CurrentTarget._geoPosition;
				return;
			}
			if ((double)num > 0.0001 && (_ap._targetMemory || _ap._guidanceType == AmmunitionParameters.GuidanceType.None || base._flightStage != FlightStage.TerminalApproach || !_seeker.isActive()))
			{
				_currentKnownTargetPosition = base.InitialTargetGeoPosition.ToUnity() + _lastKnownTargetVelocity * num;
				float num3 = _insDriftValue * _ap._insDrift / 60f * num;
				if (num3 != 0f)
				{
					Vector3 vector = _launchPosition.ToUnity();
					Vector3 vector2 = _currentKnownTargetPosition - vector;
					vector2.y = 0f;
					_currentKnownTargetPosition = vector + Quaternion.Euler(0f, num3, 0f) * (_currentKnownTargetPosition - vector);
				}
				if (!base.CurrentTarget.IsDestroyed)
				{
					_currentKnownTargetPosition.y = base.CurrentTarget.VisibleAltitude * num2 + _currentKnownTargetPosition.y * (1f - num2);
				}
				return;
			}
		}
		else if (flag)
		{
			_currentKnownTargetPosition = base.InitialTargetGeoPosition.ToUnity();
			float num4 = GameTime.time - _launchTime;
			float num5 = _insDriftValue * _ap._insDrift / 60f * num4;
			if (num5 != 0f)
			{
				Vector3 vector3 = _launchPosition.ToUnity();
				_currentKnownTargetPosition = vector3 + Quaternion.Euler(0f, num5, 0f) * (_currentKnownTargetPosition - vector3);
			}
			if (Vector3.Distance(_currentKnownTargetPosition, base.transform.position) < _ap._terminalApproachDist * 2f && _ap._requiresTargetToProceed)
			{
				Vector3 vector4 = base.transform.position + (_currentKnownTargetPosition - base.transform.position).normalized * (_ap._terminalApproachDist * 2f);
				base.InitialTargetGeoPosition = Utils.worldPositionFromUnityToLongLat(new Vector3(vector4.x, 0f, vector4.z), Globals._currentCenterTile);
				CreateWaypoints();
			}
			return;
		}
		_currentKnownTargetPosition = base.transform.position + base.transform.forward * 10f;
		_lastKnownTargetVelocity = Vector3.zero;
	}

	public void CreateWaypoints()
	{
		Waypoints.Clear();
		_lastWaypointPosition = _launchPosition;
		bool selectivePopUpDisabled = false;
		if (_ap._terminalPopUpDist > 0f && _ap._selectivePopUp && base.CurrentTarget != null)
		{
			if (base.CurrentTarget is Vessel vessel)
			{
				if (vessel.VP._displacement > 420f)
				{
					selectivePopUpDisabled = true;
				}
			}
			else if (!(base.CurrentTarget is Submarine))
			{
				selectivePopUpDisabled = true;
			}
		}
		foreach (WaypointConfig item in CreateWaypointConfigs(new WaypointCreationContext
		{
			Ap = _ap,
			IsAirTarget = (((bool)base.CurrentTarget && base.CurrentTarget.isAirTarget()) || (!base.CurrentTarget && _ap._targetType == Ammunition.Target.AAW)),
			TerrainFollow = _ap._terrainFollowFlight,
			IsGroupLeader = GroupLeader,
			StartAngle = MathF.Asin(base.transform.forward.normalized.y) * 57.29578f,
			SelectivePopUpDisabled = selectivePopUpDisabled,
			LaunchAltUnity = _launchPosition.y
		}))
		{
			Waypoints.Add(new Waypoint(new Waypoint.DistanceToTargetWaypoint(item.DistanceToTarget), this, item.Settings));
		}
		if (_ap.Kinematics != AmmunitionParameters.KinematicsLevel.None)
		{
			TuneWaypoints();
		}
	}

	private void TuneWaypoints()
	{
		_tuneWaypointsDisposable?.Dispose();
		_tuneWaypointsDisposable = null;
		List<Waypoint> kappaWaypoints = Waypoints.Where((Waypoint w) => w.WpSettings.PitchMode == Waypoint.ToHeightType.KappaLoft && w.WpSettings.LoftHeight.HasValue).ToList();
		if (kappaWaypoints.Count == 0)
		{
			return;
		}
		AmmunitionParameters ap = _ap;
		Vector3 launchPos = _launchPosition.ToUnity();
		Vector3 targetPos = base.AimPointGeoPosition.ToUnity();
		Vector3 targetVel = ((base.CurrentTarget != null) ? base.CurrentTarget.UnityVelocityVector : Vector3.zero);
		bool isAirUnit = _airLaunched;
		float platformSpd = ((_launchPlatform != null) ? _launchPlatform.getVelocityInKnots() : 0f);
		float dragFactor = ap.GetDragFactor(isAirUnit);
		float num = Mathf.Max(launchPos.y, targetPos.y);
		float altMax = kappaWaypoints.Max((Waypoint w) => w.WpSettings.LoftHeight.Value.Altitude);
		if (altMax <= num)
		{
			return;
		}
		_tuneWaypointsDisposable = Observable.Start(() => SearchOptimalLoftAltitude(ap, launchPos, targetPos, targetVel, platformSpd, isAirUnit, dragFactor, launchPos.y, altMax)).ObserveOnMainThread().Subscribe(delegate(float optimalAlt)
		{
			if (this == null || base.IsDestroyed || kappaWaypoints.Any((Waypoint wp) => !Waypoints.Contains(wp)))
			{
				return;
			}
			foreach (Waypoint item in kappaWaypoints)
			{
				Waypoint.HeightSettings value = item.WpSettings.LoftHeight.Value;
				value.Altitude = optimalAlt;
				item.WpSettings.LoftHeight = value;
				item.InvalidateLoftCache();
			}
		});
	}

	public static float SearchOptimalLoftAltitude(AmmunitionParameters ap, Vector3 launchPos, Vector3 targetPos, Vector3 targetVel, float platformSpeedKnots, bool isAirUnit, float dragFactor, float altMin, float altMax)
	{
		KinematicRangeResult kinematicRangeResult = EvaluateLoftAltitude(ap, launchPos, targetPos, targetVel, platformSpeedKnots, isAirUnit, dragFactor, altMax);
		KinematicRangeResult other = EvaluateLoftAltitude(ap, launchPos, targetPos, targetVel, platformSpeedKnots, isAirUnit, dragFactor, altMin);
		float num = altMin;
		float num2 = altMax;
		for (int i = 0; i < 10; i++)
		{
			float num3 = (num + num2) * 0.5f;
			KinematicRangeResult kinematicRangeResult2 = EvaluateLoftAltitude(ap, launchPos, targetPos, targetVel, platformSpeedKnots, isAirUnit, dragFactor, num3);
			if (kinematicRangeResult.CompareTo(other) >= 0)
			{
				other = kinematicRangeResult2;
				num = num3;
			}
			else
			{
				kinematicRangeResult = kinematicRangeResult2;
				num2 = num3;
			}
		}
		if (kinematicRangeResult.CompareTo(other) < 0)
		{
			return num;
		}
		return num2;
	}

	private static KinematicRangeResult EvaluateLoftAltitude(AmmunitionParameters ap, Vector3 launchPos, Vector3 targetPos, Vector3 targetVel, float platformSpeedKnots, bool isAirUnit, float dragFactor, float loftAlt)
	{
		return SimulateShotLinear(ap, launchPos, platformSpeedKnots, targetVel, targetPos, isAirUnit, dragFactor, -1f, -1f, null, null, null, loftAlt);
	}

	public static Vector3 ComputePN(Vector3 missileVel, Vector3 relPos, Vector3 relVel, float N)
	{
		float magnitude = relPos.magnitude;
		if (magnitude < 0.001f)
		{
			return Vector3.zero;
		}
		Vector3 vector = relPos / magnitude;
		Vector3 vector2 = (relVel - Vector3.Dot(relVel, vector) * vector) / magnitude;
		float num = 0f - Vector3.Dot(relVel, vector);
		return N * num * vector2;
	}

	public static float CalculateDrag(float altitude, float velocity, float time, float pitch, float dragFactor, bool motorBurning, float targetAltitude = 0f, float liftFactor = 0.005f, float stallSpeedKnots = 0f, float pitchRateDegPerSec = 0f)
	{
		float aeroDragKnots;
		float inducedDragKnots;
		float parallelGKnots;
		return CalculateDrag(altitude, velocity, time, pitch, dragFactor, liftFactor, motorBurning, targetAltitude, out aeroDragKnots, out inducedDragKnots, out parallelGKnots);
	}

	public static float CalculateDrag(float altitude, float velocity, float time, float pitch, float dragFactor, float liftFactor, bool motorBurning, float targetAltitude, out float aeroDragKnots, out float inducedDragKnots, out float parallelGKnots)
	{
		float num = (float)Utils.CalculateAirDensity(altitude) * velocity * velocity * 0.2f * time * dragFactor;
		float num2 = MathF.PI / 180f * pitch;
		float num3 = 9.81f;
		float num4 = num3 * Mathf.Sin(0f - num2);
		float num5 = 0f;
		if (!motorBurning)
		{
			float num6 = Mathf.Max((float)Utils.CalculateAirDensity(targetAltitude) / 1.225f, 0.001f);
			num5 = MathF.Sqrt(MathF.Abs(Mathf.Cos(num2))) * dragFactor * liftFactor * num3 / num6;
		}
		float num7 = (num5 + num4) * 1.94384f * time;
		aeroDragKnots = num;
		inducedDragKnots = num5 * 1.94384f * time;
		parallelGKnots = num4 * 1.94384f * time;
		return num + num7;
	}

	public override float CalculateHitChance(ObjectBase target)
	{
		if (target == null)
		{
			return 0f;
		}
		float num = Utils.GetInterceptionChance(target, this).hitChance;
		if (target._obp != null && target._obp._sensorSystems != null)
		{
			float num2 = 0f;
			foreach (SensorSystem sensorSystem in target._obp._sensorSystems)
			{
				if (!(sensorSystem is SensorSystemECM sensorSystemECM) || sensorSystem.IsDestroyed || (sensorSystemECM._ecm._ep._type != ECMParameters.ECMType.Defensive && sensorSystemECM._ecm._ep._type != ECMParameters.ECMType.Universal))
				{
					continue;
				}
				Globals.Frequency frequency = Globals.Frequency.Undefined;
				if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand && _guidingRadarSystem != null)
				{
					frequency = _guidingRadarSystem._radar._rp._frequency;
				}
				if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.ActiveRadarHoming)
				{
					frequency = _ap._frequency;
				}
				else if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && _guidingRadarSystem != null)
				{
					frequency = _guidingRadarSystem._radar._rp._frequency;
				}
				if (frequency != Globals.Frequency.Undefined && sensorSystemECM._ecm._ep._frequencies.Contains(frequency))
				{
					float num3 = Mathf.Clamp01(sensorSystemECM._ecm._ep._jamChance - _ap._antiJammerBonus);
					if (num3 > num2)
					{
						num2 = num3;
					}
				}
			}
			num *= 1f - num2;
		}
		float num4 = 0f;
		if (target.AmmunitionNameToAmmunitionDictionary != null)
		{
			foreach (Ammunition value in target.AmmunitionNameToAmmunitionDictionary.Values)
			{
				if (value?._ap != null && (value._ap._type == Ammunition.Type.Chaff || value._ap._type == Ammunition.Type.Noisemaker))
				{
					num4 = Mathf.Clamp01(value._ap._decoySpoofChance - _ap._antiChaffBonus);
					break;
				}
			}
		}
		num *= 1f - num4;
		return Mathf.Clamp01(num);
	}

	private void ScheduleFlightEffects(bool stopBoosterAudioFirst = false)
	{
		if (_flightEffects == null)
		{
			_flightStartTime = GameTime.time;
			_boosterEffectStartTime = _flightStartTime;
			_isBoosterEffectStarted = true;
			_stopBoosterAudioFirst = stopBoosterAudioFirst;
			bool num = (_ap._sustainerEffectStartTime > 0f || _ap._sustainerBurnTime > 0f) && _ap._sustainerEffect._isAvailable;
			List<MissileFlightEffect> list = new List<MissileFlightEffect>
			{
				new MissileFlightEffect
				{
					effectDef = _ap._boosterEffect,
					type = MissileEffectType.Booster,
					startDelay = 0f,
					endDelay = -1f
				}
			};
			if (num)
			{
				float sustainerEffectStartTime = _ap._sustainerEffectStartTime;
				float endDelay = ((_ap._sustainerBurnTime > 0f) ? (sustainerEffectStartTime + _ap._sustainerBurnTime) : ((!(_ap._timeToMaxRange > 10f)) ? (-1f) : (_ap._timeToMaxRange - 10f)));
				list.Add(new MissileFlightEffect
				{
					effectDef = _ap._sustainerEffect,
					type = MissileEffectType.Sustainer,
					startDelay = sustainerEffectStartTime,
					endDelay = endDelay
				});
			}
			list.Add(new MissileFlightEffect
			{
				effectDef = _ap._inFlightEffect,
				type = MissileEffectType.InFlight,
				startDelay = _ap._inFlightEffectStartTime,
				endDelay = -1f
			});
			_flightEffects = list.ToArray();
		}
	}

	private void UpdateFlightEffects()
	{
		if (_flightEffects == null)
		{
			return;
		}
		float num = GameTime.time - _flightStartTime;
		for (int i = 0; i < _flightEffects.Length; i++)
		{
			MissileFlightEffect missileFlightEffect = _flightEffects[i];
			if (missileFlightEffect.started && !(missileFlightEffect.instance == null) && !(missileFlightEffect.endDelay <= 0f) && !(missileFlightEffect.endDelay - missileFlightEffect.startDelay > GameTime.time - missileFlightEffect.startTime))
			{
				ParticleSystem component = missileFlightEffect.instance.GetComponent<ParticleSystem>();
				if (component != null)
				{
					component.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
				}
				missileFlightEffect.instance = null;
			}
		}
		for (int j = 0; j < _flightEffects.Length; j++)
		{
			MissileFlightEffect missileFlightEffect2 = _flightEffects[j];
			if (missileFlightEffect2.started || num < missileFlightEffect2.startDelay)
			{
				continue;
			}
			float duration = ((missileFlightEffect2.endDelay > 0f) ? (missileFlightEffect2.endDelay - missileFlightEffect2.startDelay) : (-1f));
			missileFlightEffect2.instance = Singleton<EffectsManager>.Instance.spawnEffect(new EffectParameters(missileFlightEffect2.effectDef, base.transform), duration);
			if (missileFlightEffect2.instance != null)
			{
				missileFlightEffect2.instance.transform.localPosition = missileFlightEffect2.effectDef._position;
				missileFlightEffect2.instance.transform.localRotation = Quaternion.identity;
				TrailRenderer component2 = missileFlightEffect2.instance.GetComponent<TrailRenderer>();
				if (component2 != null)
				{
					component2.Clear();
				}
			}
			if (missileFlightEffect2.type == MissileEffectType.Booster)
			{
				_boosterEffect = missileFlightEffect2.instance;
				if (missileFlightEffect2.instance != null && _weaponInstance._boosterAudioSource != null)
				{
					if (_stopBoosterAudioFirst)
					{
						_weaponInstance._boosterAudioSource.Stop();
					}
					_weaponInstance._boosterAudioSource.Play();
				}
				else if (missileFlightEffect2.instance == null && j + 1 < _flightEffects.Length)
				{
					_flightEffects[j + 1].startDelay = 0f;
				}
			}
			else if (missileFlightEffect2.type == MissileEffectType.InFlight)
			{
				_inFlightEffect = missileFlightEffect2.instance;
				if (missileFlightEffect2.instance != null)
				{
					if (_weaponInstance._sustainerAudioSource != null)
					{
						_weaponInstance._sustainerAudioSource.Play();
					}
					if (_weaponInstance._exhaustAudioSource != null)
					{
						_weaponInstance._exhaustAudioSource.Play();
					}
					if (_weaponInstance._farAudioSource != null)
					{
						_weaponInstance._farAudioSource.Play();
					}
				}
				_isInFlightEffectStarted = true;
			}
			missileFlightEffect2.startTime = GameTime.time;
			missileFlightEffect2.started = true;
		}
	}

	private void PerformMoveForward(Vector3 moveOfLaunchPlatform)
	{
		_lastPos = base.transform.position;
		_newPos = _lastPos;
		UpdateFlightEffects();
		if (!base.IsDestroyed)
		{
			_dropSpeed *= 0.95f;
			if (!_checkForWaterExit)
			{
				float num = 0f;
				if (_isBoosterEffectStarted)
				{
					num = CalculateThrustOverTime(_ap, _airLaunched, GameTime.time - _boosterEffectStartTime, GameTime.fixedDeltaTime) * _motorPerformance;
				}
				float num2;
				if (_inMissileGroup)
				{
					num2 = _inGroupVelocityInKnots;
				}
				else
				{
					num2 = ((base._flightStage != FlightStage.PerformSnakeSearch) ? _ap._maxVelocityInKnots : _ap._searchVelocityInKnots);
					if (base._flightStage == FlightStage.MaintainLoftAlt)
					{
						num2 = _ap._maxLoftVelocityInKnots;
					}
					else if (base._flightStage == FlightStage.TerminalApproach)
					{
						num2 = _ap._terminalVelocityInKnots;
					}
				}
				if (_ap.Kinematics != AmmunitionParameters.KinematicsLevel.None)
				{
					_velocityInKnots += num;
				}
				else if (_velocityInKnots > num2)
				{
					float x = _ap._deceleration * 9.8f * 1.94384f * Time.fixedDeltaTime;
					float y = _velocityInKnots - num2;
					_velocityInKnots -= MathF.Min(x, y);
				}
				else if (_velocityInKnots < num2 - 0.001f)
				{
					float y2 = num2 - _velocityInKnots;
					_velocityInKnots += MathF.Min(num, y2);
				}
				if (_ap.Kinematics != AmmunitionParameters.KinematicsLevel.None)
				{
					float pitchRateDegPerSec = ((GameTime.fixedDeltaTime > 0f) ? ((_currentPitch - _prevPitch) / GameTime.fixedDeltaTime) : 0f);
					float y3 = base.transform.position.y;
					if ((bool)base.CurrentTarget)
					{
						y3 = base.CurrentTarget.transform.position.y;
					}
					_velocityInKnots -= CalculateDrag(base.transform.position.y, _velocityInUnity, GameTime.fixedDeltaTime, _currentPitch, _ap.GetDragFactor(_airLaunched), num > 0f, y3, _ap.LiftFactor, _ap.MinVelocity, pitchRateDegPerSec);
				}
				if (IsStalled(_ap, _velocityInKnots, num) && _isBoosterEffectStarted)
				{
					Destruction(base.transform.position, base.transform.rotation, null);
				}
			}
			else
			{
				_velocityInKnots = 45f;
			}
			float num3 = _dropSpeed * 0.0076554087f * GameTime.fixedDeltaTime;
			float num4 = _velocityInKnots * 0.0076554087f * GameTime.fixedDeltaTime;
			_ = base.transform.position;
			Vector3 forward = base.transform.forward;
			if (_maintainingHeight)
			{
				forward.y = 0f;
			}
			_newPos += num4 * forward;
			_newPos += num3 * Vector3.down;
			_newPos += moveOfLaunchPlatform;
		}
		else
		{
			_velocityInKnots = 0f;
			_velocityInUnity = 0f;
			_velocityVecInUnity = Vector3.zero;
		}
	}

	private void PerformDropping()
	{
		_lastPos = base.transform.position;
		_newPos = _lastPos;
		float num = 19.06907f * GameTime.fixedDeltaTime;
		_dropSpeed += num;
		if (_dropSpeed > 20f)
		{
			_dropSpeed = 20f;
		}
		float num2 = _dropSpeed * 0.0076554087f * GameTime.fixedDeltaTime;
		float num3 = _dropSpeed / 20f;
		float num4 = 1f - num3 / 10f;
		Vector3 vector = Vector3.zero;
		if (_launchPlatform != null)
		{
			vector = num4 * _launchPlatform.getVelocityInKnots() * _launchPlatform.getRootGameObject().transform.forward;
		}
		vector *= 0.0076554087f * GameTime.fixedDeltaTime;
		float num5 = _velocityInKnots * 0.0076554087f * GameTime.fixedDeltaTime;
		_ = base.transform.position;
		_newPos += num2 * Vector3.down;
		_newPos += vector;
		Vector3 direction = Vector3.forward * _velocityInUnity;
		_velocityVecInUnity = base.transform.TransformDirection(direction);
		_lastVelocityVector = base.transform.forward * num5;
	}

	private void JoinOrCreateMissileGroup()
	{
		if (base.CurrentTarget is LandUnit)
		{
			return;
		}
		_inMissileGroup = true;
		float num = 413.39243f;
		foreach (MissileGroup missileGroup in base._taskforce._missileGroups)
		{
			if (missileGroup._leader._ap._ammunitionFileName == _ap._ammunitionFileName && missileGroup.Count != missileGroup._maxMembers && !(missileGroup._leader._targetInterceptPosition.UnityDistance(_targetInterceptPosition) > num) && !((missileGroup._leader.transform.position - base.transform.position).sqrMagnitude > _ap._groupJoinRangeUnitySq))
			{
				missileGroup.Add(this);
				return;
			}
		}
		new MissileGroup(this);
	}

	private bool CheckForJoinMissileGroup()
	{
		if (base.CurrentTarget is LandUnit)
		{
			return false;
		}
		float num = 413.39243f;
		foreach (MissileGroup missileGroup in base._taskforce._missileGroups)
		{
			if (missileGroup._leader._ap._ammunitionFileName == _ap._ammunitionFileName && missileGroup.Count != missileGroup._maxMembers && !(missileGroup._leader.AimPointGeoPosition.UnityDistance(base.AimPointGeoPosition) > num) && !((missileGroup._leader.transform.position - base.transform.position).sqrMagnitude > _ap._groupJoinRangeUnitySq))
			{
				missileGroup.Add(this);
				return true;
			}
		}
		return false;
	}

	public static float? CalculateDesiredAltitude(FlightProfileContext context, Vector3 targetPosition, float currentAltitude, float dist)
	{
		float? result = GetBaseAltitudeByStage(context, targetPosition, dist);
		if (result.HasValue && !context.IgnoreSeekerFov)
		{
			result = ApplySeekerFOVConstraint(context, result.Value, targetPosition);
		}
		return result;
	}

	private static float? GetBaseAltitudeByStage(FlightProfileContext context, Vector3 targetPosition, float dist)
	{
		float y = context.MissilePosition.y;
		switch (context.Stage)
		{
		case FlightStage.MoveToSeaSkimming:
		case FlightStage.MaintainSeaSkimming:
			if (context.UsingSubmunition)
			{
				return context.Ap._seaSkimmingSubAltUnity;
			}
			return context.Ap._seaSkimmingAltUnity;
		case FlightStage.ToBearing:
		case FlightStage.MoveToLoftAlt:
		case FlightStage.MaintainLoftAlt:
		{
			if (context.CruiseMissile)
			{
				return context.Ap._maxLoftAltUnity;
			}
			Vector2 vector = new Vector2(context.LaunchPosition.x, context.LaunchPosition.z);
			Vector2 vector2 = new Vector2(targetPosition.x, targetPosition.z);
			Vector2 vector3 = new Vector2(context.MissilePosition.x, context.MissilePosition.z);
			float magnitude = (vector2 - vector).magnitude;
			float magnitude2 = (vector3 - vector).magnitude;
			float a = MathF.Tan(Mathf.Clamp(context.Ap._maxLoftAngle, 0.01f, 89f) * (MathF.PI / 180f));
			float a2 = MathF.Tan(Mathf.Clamp(context.Ap._finalFlightPhaseMaxAngle, 0.01f, 89f) * (MathF.PI / 180f));
			float y2 = context.LaunchPosition.y;
			float y3 = targetPosition.y;
			float maxLoftAltUnity = context.Ap._maxLoftAltUnity;
			float num = Mathf.Max(maxLoftAltUnity, y2, y3);
			float b = (num - y2) / magnitude;
			float b2 = (num - y3) / magnitude;
			a = Mathf.Clamp(Mathf.Max(a, b), 0.01f, 99f);
			a2 = Mathf.Clamp(Mathf.Max(a2, b2), 0.01f, 99f);
			float num2 = 1f / a;
			float num3 = 1f / a2;
			float b3 = (magnitude + y2 * num2 + y3 * num3) / (num2 + num3);
			float num4 = Mathf.Min(maxLoftAltUnity, b3);
			num4 = Mathf.Max(num4, y2, y3);
			float num5 = (num4 - y2) * num2;
			float num6 = (num4 - y3) * num3;
			float num7 = num5 / (num5 + num6 + 1E-05f);
			float num8 = magnitude * num7;
			if (magnitude2 < num8)
			{
				return Mathf.Lerp(y2, num4, MathF.Pow(magnitude2 / num8, 0.5f));
			}
			return Mathf.Lerp(num4, y3, MathF.Pow((magnitude2 - num8) / (magnitude - num8), 1.3f));
		}
		case FlightStage.MoveToFinalFlightAlt:
		case FlightStage.MaintainFinalFlightAlt:
			if (context.GroupLeader)
			{
				return context.Ap._groupLeaderAltUnity;
			}
			if (context.GroupWingman)
			{
				return context.Ap._groupWingmanAltUnity;
			}
			if (context.Ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Launcher)
			{
				return context.LaunchPosition.y;
			}
			if (context.Ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Target && context.TargetAltitude.HasValue)
			{
				return context.TargetAltitude.Value;
			}
			if (context.Ap._finalFlightPhaseAltMode == AmmunitionParameters.LoftMode.Maintain)
			{
				return context.MissilePosition.y;
			}
			return context.Ap._finalFlightPhaseAltUnity;
		case FlightStage.TerminalApproach:
			if (context.UsingSubmunition)
			{
				return context.Ap._seaSkimmingSubAltUnity;
			}
			if (context.Ap._terminalAltMode == AmmunitionParameters.LoftMode.Launcher)
			{
				return context.LaunchPosition.y;
			}
			if (context.Ap._terminalAltMode == AmmunitionParameters.LoftMode.Target && context.TargetAltitude.HasValue)
			{
				return context.TargetAltitude.Value;
			}
			if (context.Ap._terminalAltMode == AmmunitionParameters.LoftMode.Maintain)
			{
				return context.MissilePosition.y;
			}
			if (context.IsAirTarget && context.TargetAltitude.HasValue)
			{
				return context.TargetAltitude.Value;
			}
			if (context.Ap._terminalDiveDistance > dist)
			{
				return context.TargetAltitude;
			}
			if (context.Ap._terminalAltUnity > 0f)
			{
				return context.Ap._terminalAltUnity;
			}
			if (context.Ap._finalFlightPhaseDistToTarget > 0f && context.Ap._finalFlightPhaseAltUnity > 0f)
			{
				return context.Ap._finalFlightPhaseAltUnity;
			}
			if (context.Ap._seaSkimmingStartDistToTargetUnity > 0f && context.Ap._seaSkimmingAltUnity > 0f)
			{
				return context.Ap._seaSkimmingAltUnity;
			}
			if (context.TargetAltitude.HasValue)
			{
				return context.TargetAltitude.Value;
			}
			return context.Ap._currentMinFlightAltUnity;
		case FlightStage.MaintainHeight:
			return (context.Ap._currentMinFlightAltUnity > 0f) ? context.Ap._currentMinFlightAltUnity : context.MissilePosition.y;
		default:
			return y;
		}
	}

	private static float ApplySeekerFOVConstraint(FlightProfileContext context, float desiredAlt, Vector3 targetPosition)
	{
		if (!context.SeekerActive || !context.SeekerFOV.HasValue)
		{
			return desiredAlt;
		}
		float value = context.SeekerFOV.Value;
		if (value <= 0f)
		{
			return desiredAlt;
		}
		Vector3 vector = targetPosition - context.MissilePosition;
		float magnitude = vector.magnitude;
		if (magnitude <= 1f)
		{
			return desiredAlt;
		}
		float num = (0f - Mathf.Asin(vector.y / magnitude)) * 57.29578f;
		float num2 = desiredAlt - context.MissilePosition.y;
		float num3 = (0f - Mathf.Atan(num2 / Mathf.Min(magnitude / 2f, 4f))) * 57.29578f;
		float max = num + value * 0.4f;
		float min = num - value * 0.4f;
		float num4 = Mathf.Clamp(num3, min, max);
		if (num4 != num3)
		{
			return context.MissilePosition.y + num2 * num4 / num3;
		}
		return desiredAlt;
	}

	public static (float interceptTime, bool canIntercept, Vector3 interceptPosition) CalculateMissileInterceptTime(AmmunitionParameters ap, Vector3 launchPosition, float launchVelocityInKnots, float launchAltitudeUnity, Vector3 targetPosition, Vector3 targetVelocity, bool isAirLaunched = false)
	{
		launchPosition.y = launchAltitudeUnity;
		float b = ap._maxVelocityInKnots * 0.0076554087f;
		float accelerationTime = ap._accelerationTime;
		float sustainerBurnTime = ap._sustainerBurnTime;
		float num = accelerationTime + sustainerBurnTime;
		float num2 = ap._acceleration * 9.81f * 0.0148809375f;
		float num3 = ap._sustainerBurnAcceleration * 9.81f * 0.0148809375f;
		float num4 = (launchVelocityInKnots + ap._ejectVelocity) * 0.0076554087f;
		Vector3 vector = targetPosition - launchPosition;
		_ = vector.magnitude;
		Vector3 normalized = vector.normalized;
		float dragFactor = ap.GetDragFactor(isAirLaunched);
		float num5 = 0.5f;
		float num6 = ((ap._maxFlightTime > 0f) ? ap._maxFlightTime : 600f);
		float num7 = 0f;
		Vector3 vector2 = launchPosition;
		float num8 = num4;
		Vector3 vector3 = normalized;
		float num9 = float.MaxValue;
		float item = 0f;
		Vector3 item2 = targetPosition;
		while (num7 < num6)
		{
			Vector3 vector4 = targetPosition + targetVelocity * num7;
			Vector3 vector5 = vector4 - vector2;
			float magnitude = vector5.magnitude;
			if (magnitude < num8 * num5 * 1.5f)
			{
				float num10 = num7 + magnitude / num8;
				Vector3 item3 = targetPosition + targetVelocity * num10;
				return (interceptTime: num10, canIntercept: true, interceptPosition: item3);
			}
			if (magnitude < num9)
			{
				num9 = magnitude;
				item = num7;
				item2 = vector4;
			}
			else if (num7 > num + 5f && magnitude > num9 * 1.2f)
			{
				return (interceptTime: item, canIntercept: false, interceptPosition: item2);
			}
			if (num7 < accelerationTime)
			{
				num8 += num2 * num5 * 1.2f;
			}
			else if (num7 < num)
			{
				num8 += num3 * num5 * 1.2f;
			}
			if (num7 >= num && dragFactor > 0f)
			{
				float num11 = Mathf.Clamp01(1f - vector2.y * 220.47266f / 100000f);
				float num12 = dragFactor * num11 * num8 * 0.001f;
				num8 = Mathf.Max(num8 - num12 * num5, ap.MinVelocity * 0.0076554087f);
			}
			num8 = Mathf.Min(num8, b);
			vector3 = Vector3.Lerp(vector3, vector5.normalized, 0.1f).normalized;
			vector2 += vector3 * num8 * num5;
			num7 += num5;
			if (num8 < ap.MinVelocity * 0.0076554087f)
			{
				return (interceptTime: item, canIntercept: false, interceptPosition: item2);
			}
		}
		return (interceptTime: item, canIntercept: false, interceptPosition: item2);
	}

	public static (float interceptTime, bool canIntercept, Vector3 interceptPosition) CalculateMissileInterceptTimeAnalytical(AmmunitionParameters ap, Vector3 launchPosition, float launchVelocityInKnots, Vector3 targetPosition, Vector3 targetVelocity, float dragFactor)
	{
		Vector3 toTarget = targetPosition - launchPosition;
		float num = (launchVelocityInKnots + ap._ejectVelocity) * 0.0076554087f + (ap._acceleration * ap._accelerationTime + ap._sustainerBurnTime * ap._sustainerBurnAcceleration) * 9.8f * 0.0148809375f;
		float num2 = SolveInterceptQuadratic(num, toTarget, targetVelocity);
		if (num2 < 0f)
		{
			return (interceptTime: -1f, canIntercept: false, interceptPosition: targetPosition);
		}
		float num3 = SolveInterceptQuadratic(DragCorrectedAvgSpeed(num, num / 0.0076554087f, num2, ap, dragFactor, launchPosition.y, targetPosition.y), toTarget, targetVelocity);
		if (num3 < 0f)
		{
			return (interceptTime: -1f, canIntercept: false, interceptPosition: targetPosition);
		}
		Vector3 item = targetPosition + targetVelocity * num3;
		return (interceptTime: num3, canIntercept: true, interceptPosition: item);
	}

	private static float SolveInterceptQuadratic(float missileSpeed, Vector3 toTarget, Vector3 targetVelocity)
	{
		float num = missileSpeed * missileSpeed;
		float num2 = targetVelocity.sqrMagnitude - num;
		float num3 = 2f * Vector3.Dot(toTarget, targetVelocity);
		float sqrMagnitude = toTarget.sqrMagnitude;
		if (Mathf.Abs(num2) < 0.0001f)
		{
			if (Mathf.Abs(num3) < 0.0001f)
			{
				return -1f;
			}
			float num4 = (0f - sqrMagnitude) / num3;
			if (!(num4 > 0f))
			{
				return -1f;
			}
			return num4;
		}
		float num5 = num3 * num3 - 4f * num2 * sqrMagnitude;
		if (num5 < 0f)
		{
			return -1f;
		}
		float num6 = Mathf.Sqrt(num5);
		float num7 = (0f - num3 + num6) / (2f * num2);
		float num8 = (0f - num3 - num6) / (2f * num2);
		if (num7 > 0f && num8 > 0f)
		{
			return Mathf.Min(num7, num8);
		}
		if (num7 > 0f)
		{
			return num7;
		}
		if (num8 > 0f)
		{
			return num8;
		}
		return -1f;
	}

	private static float EstimateMissileDistance(float time, float burnTime, float initialVelocity, float maxVelocity, float minVelocity, float distanceDuringBurn, float coastRange)
	{
		if (time <= 0f)
		{
			return 0f;
		}
		if (time <= burnTime)
		{
			float num = (maxVelocity - initialVelocity) / burnTime;
			return initialVelocity * time + 0.5f * num * time * time;
		}
		float num2 = time - burnTime;
		float num3 = (maxVelocity + minVelocity) / 2f * num2;
		if (coastRange > 0.001f)
		{
			float num4 = maxVelocity - minVelocity;
			float num5 = num4 / coastRange;
			_ = maxVelocity / num5;
			float num6 = 1f - Mathf.Exp((0f - num2) * num5 / maxVelocity);
			num3 = (maxVelocity - num4 * num6 * 0.5f) * num2;
		}
		return distanceDuringBurn + num3;
	}

	private static float CalculateEffectiveVelocity(float distance, float burnTime, float initialVelocity, float maxVelocity, float minVelocity, float distanceDuringBurn, float avgAccelVelocity, float avgCoastVelocity, float coastRange)
	{
		if (distance <= 0.001f)
		{
			return maxVelocity;
		}
		if (distance <= distanceDuringBurn)
		{
			float num = distance / distanceDuringBurn;
			float num2 = initialVelocity + (maxVelocity - initialVelocity) * num;
			return (initialVelocity + num2) / 2f;
		}
		float num3 = distance - distanceDuringBurn;
		float num4 = Mathf.Clamp01(num3 / Mathf.Max(coastRange, 0.001f));
		float num5 = maxVelocity - (maxVelocity - minVelocity) * num4;
		float num6 = (maxVelocity + num5) / 2f;
		float num7 = distanceDuringBurn / distance;
		float num8 = num3 / distance;
		return avgAccelVelocity * num7 + num6 * num8;
	}

	public static float CalculateMissileInterceptTimeSimple(AmmunitionParameters ap, Vector3 launchPosition, Vector3 targetPosition, Vector3 targetVelocity)
	{
		Vector3 toTarget = targetPosition - launchPosition;
		return SolveInterceptQuadratic(ap._maxVelocityInKnots * 0.0076554087f, toTarget, targetVelocity);
	}

	private void SetMaxPitchAngle(FlightStage stage)
	{
		_maxPitch = GetMaxPitchAngle(stage, _ap);
	}

	public static float GetMaxPitchAngle(FlightStage stage, AmmunitionParameters ap)
	{
		switch (stage)
		{
		case FlightStage.MoveToSeaSkimming:
		case FlightStage.MaintainSeaSkimming:
			return ap._seaSkimmingMaxDescentAngle;
		case FlightStage.ToBearing:
		case FlightStage.MaintainHeight:
		case FlightStage.MoveToLoftAlt:
		case FlightStage.MaintainLoftAlt:
			return ap._maxLoftAngle;
		case FlightStage.MoveToFinalFlightAlt:
		case FlightStage.MaintainFinalFlightAlt:
			return ap._finalFlightPhaseMaxAngle;
		case FlightStage.Launch:
			return 90f;
		case FlightStage.TerminalApproach:
			return ap._seaSkimmingMaxDescentAngle;
		default:
			return 90f;
		}
	}

	private void CheckRadioConnection()
	{
		if (_launchPlatform == null)
		{
			DM.I(getUIDAndName() + ": Launch platform is null object.");
			base.ConnectionLost.Value = true;
			return;
		}
		if (!_guidingRadarSystem.IsGuidingWeapon(this))
		{
			if (!_guidingRadarSystem.TryToAddWeapon(this))
			{
				base.ConnectionLost.Value = true;
				return;
			}
			bool flag = false;
			foreach (AlignUnitToTarget alignmentTask in _guidingRadarSystem._baseObject._alignmentTasks)
			{
				if (alignmentTask._guidedWeapon == this)
				{
					alignmentTask._endAlignmentDelay = -1000f;
					alignmentTask._alignmentEndRequested = false;
					flag = true;
					break;
				}
			}
			if (flag)
			{
			}
		}
		bool value = false;
		if (base.CurrentIntendedTargetObject != null && base.CurrentIntendedTargetObject._type != ObjectType.Chaff && !_guidingRadarSystem.IsAimedAtTarget(base.CurrentIntendedTargetObject) && _ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			value = true;
			if (base._taskforce?.PlottingTable?.VehicleForObject(base.CurrentIntendedTargetObject) != null && _guidingRadarSystem.TryToAddTarget(base.CurrentIntendedTargetObject))
			{
				value = false;
			}
		}
		base.ConnectionLost.Value = value;
		ObjectBase objectBase = this;
		if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			objectBase = this;
		}
		else if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
		{
			objectBase = base.CurrentIntendedTargetObject;
			_seeker.setCurrentTarget(objectBase);
		}
		Vector3 position = _guidingRadarSystem._mountObject.transform.position;
		Vector3 position2 = objectBase.transform.position;
		_currentRadarHorizon = RadarCalculator.getRadarHorizon(_guidingRadarSystem._mountObject.transform.position.y, objectBase.VisibleAltitude, visual: false, Utils.TerrainAltForHorizon(_guidingRadarSystem._baseObject, this));
		float magnitude = (position - position2).magnitude;
		position.y = position2.y;
		float magnitude2 = (position - position2).magnitude;
		float num = _guidingRadarSystem._radar._rp._maxRange;
		if (num > _currentRadarHorizon)
		{
			num = _currentRadarHorizon;
		}
		if (magnitude2 > num)
		{
			base.ConnectionLost.Value = true;
			return;
		}
		Vector3 direction = position2 - _guidingRadarSystem._mountObject.transform.position;
		int num2 = Physics.RaycastNonAlloc(_guidingRadarSystem._mountObject.transform.position, direction, _hitAll, magnitude + 0.1f);
		ObjectBase objectBase2 = null;
		for (int i = 0; i < num2; i++)
		{
			if (_hitAll[i].collider.name == "blunt collider")
			{
				continue;
			}
			ObjectBase componentInChildren = _hitAll[i].transform.root.gameObject.GetComponentInChildren<ObjectBase>();
			if (componentInChildren == null || (!componentInChildren.isUnit() && componentInChildren._type != ObjectType.Missile && componentInChildren._type != ObjectType.Bomb && componentInChildren._type != ObjectType.Chaff) || (componentInChildren is Bomb bomb && bomb._ap._guidanceType == AmmunitionParameters.GuidanceType.None) || componentInChildren == _launchPlatform)
			{
				continue;
			}
			if (componentInChildren._type == ObjectType.Missile)
			{
				if ((_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand && componentInChildren != this) || (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && _ap._midCourseCorrection != AmmunitionParameters.MidCourseCorrection.RadioCommand && !_guidingRadarSystem.IsAimedAtTarget(componentInChildren)))
				{
					continue;
				}
			}
			else if (componentInChildren._type == ObjectType.Chaff && _seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming && _seeker.getLockedOnCountermeasure() != componentInChildren && _seeker._ignoredObjects.Contains(componentInChildren))
			{
				continue;
			}
			objectBase2 = componentInChildren;
			break;
		}
		if (objectBase2 == null)
		{
			base.ConnectionLost.Value = true;
			return;
		}
		if (objectBase2 != objectBase)
		{
			if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				if (GameTime.time - _lastRayDebugLogTime > 1f)
				{
					_lastRayDebugLogTime = GameTime.time;
				}
				base.ConnectionLost.Value = true;
				return;
			}
			if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
			{
				if (_seeker.getLockedOnCountermeasure() != null)
				{
					_seeker.setCurrentTarget(_seeker.getLockedOnCountermeasure());
					base.CurrentIntendedTargetObject = _seeker.getLockedOnCountermeasure();
				}
				else if (_seeker.CheckCountermeasureLock(objectBase2))
				{
					_seeker.setCurrentTarget(objectBase2);
				}
			}
		}
		_jammed = false;
		if (_guidingRadarSystem._baseObject._ECMSystemsTryingToJamMe.Count > 0)
		{
			SensorEcho sensorEcho = Globals._sensorEchoes.getFreeInstance();
			float waveLength = _guidingRadarSystem._radar._rp._waveLength;
			RadarParameters rp = _guidingRadarSystem._radar._rp;
			if (!RadarCalculator.createRadarEcho(_guidingRadarSystem._radar.UnityPosition, base.CurrentIntendedTargetObject, waveLength, rp._gainFactor, rp._peakPower, _guidingRadarSystem._baseObject._ECMSystemsTryingToJamMe, rp._frequency, _guidingRadarSystem._baseObject._crew, out sensorEcho, ignoreAspect: true))
			{
				_jammed = true;
			}
		}
		if (_jammed)
		{
			base.ConnectionLost.Value = true;
		}
		else if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.None && _ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
		{
			RadioCommandTargetIllumination();
		}
	}

	private void RadioCommandTargetIllumination()
	{
		if (base.CurrentIntendedTargetObject == null || _guidingRadarSystem == null)
		{
			return;
		}
		ObjectBase currentIntendedTargetObject = base.CurrentIntendedTargetObject;
		Vector3 position = _guidingRadarSystem._mountObject.transform.position;
		Vector3 position2 = currentIntendedTargetObject.transform.position;
		_currentRadarHorizon = RadarCalculator.getRadarHorizon(_guidingRadarSystem._mountObject.transform.position.y, currentIntendedTargetObject.VisibleAltitude, visual: false, Utils.TerrainAltForHorizon(_guidingRadarSystem._baseObject, currentIntendedTargetObject));
		float magnitude = (position - position2).magnitude;
		position.y = position2.y;
		if ((position - position2).magnitude > _currentRadarHorizon)
		{
			return;
		}
		Vector3 direction = position2 - _guidingRadarSystem._mountObject.transform.position;
		int num = Physics.RaycastNonAlloc(_guidingRadarSystem._mountObject.transform.position, direction, _hitAll, magnitude + 0.1f);
		ObjectBase objectBase = null;
		for (int i = 0; i < num; i++)
		{
			if (_hitAll[i].collider.name == "blunt collider")
			{
				continue;
			}
			ObjectBase componentInChildren = _hitAll[i].transform.root.gameObject.GetComponentInChildren<ObjectBase>();
			if (componentInChildren == null || (!componentInChildren.isUnit() && componentInChildren._type != ObjectType.Missile && componentInChildren._type != ObjectType.Chaff) || componentInChildren == _launchPlatform)
			{
				continue;
			}
			if (componentInChildren._type == ObjectType.Missile)
			{
				if (!_guidingRadarSystem.IsAimedAtTarget(componentInChildren))
				{
					continue;
				}
			}
			else if (componentInChildren._type == ObjectType.Chaff && _seeker.getLockedOnCountermeasure() != componentInChildren && _seeker._ignoredObjects.Contains(componentInChildren))
			{
				continue;
			}
			objectBase = componentInChildren;
			break;
		}
		if (objectBase == null || !(objectBase != currentIntendedTargetObject))
		{
			return;
		}
		if (objectBase._type == ObjectType.Chaff)
		{
			if (_seeker.CheckCountermeasureLock(objectBase))
			{
				base.CurrentIntendedTargetObject = _seeker.getLockedOnCountermeasure();
			}
		}
		else
		{
			base.CurrentIntendedTargetObject = objectBase;
		}
	}

	private void DropTorpedo()
	{
		Singleton<ObjectsManager>.Instance.removeObject(this);
		_weaponInstance._submunition._object.SetActive(value: true);
		Torpedo component = _weaponInstance._submunition._object.GetComponent<Torpedo>();
		if (component != null)
		{
			bool switchToObject = false;
			if (Singleton<RenderPosition>.Instance.getSelectedObject() == this)
			{
				switchToObject = true;
			}
			component.ASROC_Launch(this, _weaponInstance._submunition._ap, base.CurrentIntendedTargetObject, base.AimPointGeoPosition, _velocityInKnots, switchToObject);
		}
		else
		{
			Log.Error("No torpedo script attached to " + _ap._ammunitionFileName, Log.Section.General, "C:\\jenkins-agent\\workspace\\Unity Projects\\Sea Power (stable)\\Assets\\Scripts\\Weapons\\Missile.cs", 5642, "DropTorpedo");
		}
		_submunitionDropped = true;
		changeState(new SelfDestruct(this));
	}

	private bool isValidTarget(ObjectBase target)
	{
		if (target == null)
		{
			return false;
		}
		if (target.isSubmerged() && !_usingSubmunition)
		{
			return false;
		}
		return true;
	}

	public void SetNewTarget(ObjectBase newTarget)
	{
		if (base.ConnectionLost.Value || base.ConnectionLostForever.Value || !isValidTarget(newTarget) || (_guidingRadarSystem == null && !_onWire))
		{
			return;
		}
		if (_onWire)
		{
			base.CurrentIntendedTargetObject = newTarget;
			CalculateDeviation();
			CreateWaypoints();
		}
		else if (base.CurrentIntendedTargetObject != null)
		{
			if (_guidingRadarSystem.HasFreeTargetChannels())
			{
				_ = _guidingRadarSystem._targetChannels;
				_ = _guidingRadarSystem._associatedTargets.Count;
				if (_guidingRadarSystem.TryToAddTarget(newTarget))
				{
					if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
					{
						_guidingRadarSystem.setTargetObject(newTarget);
					}
					else if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
					{
						_guidingRadarSystem.setTargetObject(this);
					}
					base.CurrentIntendedTargetObject = newTarget;
					CalculateDeviation();
					CreateWaypoints();
				}
				return;
			}
			for (int i = 0; i < _guidingRadarSystem._associatedTargets.Count; i++)
			{
				if (_guidingRadarSystem._associatedTargets[i] == newTarget)
				{
					if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
					{
						_guidingRadarSystem.setTargetObject(newTarget);
					}
					else if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
					{
						_guidingRadarSystem.setTargetObject(this);
					}
					base.CurrentIntendedTargetObject = newTarget;
					CalculateDeviation();
					CreateWaypoints();
					return;
				}
			}
			ObjectBase currentIntendedTargetObject = base.CurrentIntendedTargetObject;
			int num = 0;
			if (num >= _guidingRadarSystem._associatedTargets.Count)
			{
				return;
			}
			if (_guidingRadarSystem._associatedTargets[num] == currentIntendedTargetObject)
			{
				_guidingRadarSystem._associatedTargets[num] = newTarget;
				if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
				{
					_guidingRadarSystem.setTargetObject(newTarget);
				}
				else if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
				{
					_guidingRadarSystem.setTargetObject(this);
				}
				foreach (ObjectBase associatedWeapon in _guidingRadarSystem._associatedWeapons)
				{
					if (((WeaponBase)associatedWeapon).CurrentIntendedTargetObject == currentIntendedTargetObject)
					{
						((WeaponBase)associatedWeapon).CurrentIntendedTargetObject = newTarget;
						if (associatedWeapon is Missile missile)
						{
							missile.CreateWaypoints();
						}
					}
				}
			}
			CalculateDeviation();
			CreateWaypoints();
		}
		else if (_guidingRadarSystem.TryToAddTarget(newTarget))
		{
			if (_seeker._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
			{
				_guidingRadarSystem.setTargetObject(newTarget);
			}
			else if (_ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand)
			{
				_guidingRadarSystem.setTargetObject(this);
			}
			base.CurrentIntendedTargetObject = newTarget;
			CalculateDeviation();
			CreateWaypoints();
		}
	}

	public void DestroyTestInterceptPoint()
	{
		if ((bool)_testObject)
		{
			UnityEngine.Object.Destroy(_testObject);
		}
		_waypointPathRenderer = null;
	}

	public override IWeaponControlViewModel CreateControlViewModel()
	{
		if (_ap._requiresGuidance)
		{
			return new ObjectMissileControlViewModel(this);
		}
		return null;
	}

	public override void SaveStateToFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.SaveStateToFile(ini, sectionName, isCampaignStateSave);
		if (_submunitionDropped)
		{
			ini.writeValue(sectionName, "SubmunitionDropped", _submunitionDropped);
		}
	}

	public override void LoadStateFromFile(IniHandler ini, string sectionName, bool isCampaignStateSave = false)
	{
		base.LoadStateFromFile(ini, sectionName, isCampaignStateSave);
		_submunitionDropped = ini.readValue(sectionName, "SubmunitionDropped", _submunitionDropped);
	}
}
