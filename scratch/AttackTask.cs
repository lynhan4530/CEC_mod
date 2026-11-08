using NodeCanvas.Framework;
using UnityEngine;

namespace SeaPower;

public class AttackTask : ExtActionTask
{
	public BBParameter<ObjectBase> targetObject;

	public BBParameter<GeoPosition> bearingPosition;

	public BBParameter<int> salvo;

	public BBParameter<string> ammunitionForEngage;

	public bool _isFormationAttack;

	protected override void OnExecute()
	{
		if (_displayOrderText)
		{
			if (targetObject.value != null)
			{
				string text = Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", "Engage");
				Globals._mainGameViewModel.RaiseAlert(_baseObject, targetObject.value, text);
				Singleton<VoiceMessage>.Instance.PlayVoiceSequence("EngageTrack", _baseObject, targetObject.value.GetMapContactID());
				if ((salvo.value > 1 || _baseObject.getAmmunitionByName(ammunitionForEngage.value)._ap._type == Ammunition.Type.Projectile) && Singleton<LanguageResourceHandler>.Instance.Language != "ru")
				{
					Singleton<VoiceMessage>.Instance.AddVoiceToSequence("SalvoFire", _baseObject);
				}
			}
			else
			{
				int num = Globals._rnd.Next(0, 3);
				string text2 = "RogerThat";
				switch (num)
				{
				case 1:
					text2 = "YesCommander";
					break;
				case 2:
					text2 = "Confirmed";
					break;
				}
				string alert = "${Name}: " + Singleton<LanguageResourceHandler>.Instance.getText("OrderLog", text2);
				Globals._mainGameViewModel.RaiseAlert(_baseObject, alert);
				Singleton<VoiceMessage>.Instance.PlayVoiceMessage(text2, _baseObject);
			}
		}
		int value = salvo.value;
		string text3 = ((value > 1) ? $" Salvo = {value}" : "");
		string text4 = "null";
		EngageTask engageTask = null;
		if (targetObject.value != null)
		{
			text4 = targetObject.value.getUIDAndName();
			engageTask = _baseObject.InsertEngageTask(ammunitionForEngage.value, targetObject.value, Vector3.zero, value, _baseObject.GetEngagePriority(targetObject.value), autoAttack: false, markAsReturned: false, _isFormationAttack);
		}
		else
		{
			Vector3 targetPosition = Utils.longLatToLocalV3(bearingPosition.value, Globals._currentCenterTile);
			engageTask = new EngageTask(ammunitionForEngage.value, targetPosition, _baseObject, value, 1000, autoAttack: false, _isFormationAttack);
			_baseObject.AddEngageTask(engageTask);
		}
		DM.I($"Created task: Ammo: {_baseObject._selectedAmmunitionForEngage} uid = {engageTask._uid} target = {text4}{text3}");
		finish();
	}

	protected override void OnUpdate()
	{
	}
}
