using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeaPower;
using UniRx;
using UnityEngine;

namespace SeapowerUI.ViewModels;

public class ObjectMissileControlViewModel : IWeaponControlViewModel, IDisposable, INotifyPropertyChanged
{
	private string objectState;

	public string ObjectState
	{
		get
		{
			return objectState;
		}
		set
		{
			objectState = value;
			NotifyPropertyChanged("ObjectState");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public ObjectMissileControlViewModel(Missile objectBase)
	{
		ObjectMissileControlViewModel objectMissileControlViewModel = this;
		SensorSystemLaserDesignator laser = null;
		if (objectBase._seeker._guidanceType == AmmunitionParameters.GuidanceType.LaserHoming)
		{
			laser = (objectBase._seeker as LaserSeeker).GuidingDesignator.Value;
		}
		(objectBase._seeker as LaserSeeker)?.GuidingDesignator?.Subscribe(delegate(SensorSystemLaserDesignator designator)
		{
			objectMissileControlViewModel.GetState(objectBase, designator);
		});
		objectBase.ConnectionLost.Subscribe(delegate
		{
			objectMissileControlViewModel.GetState(objectBase, laser);
		});
		objectBase.GroupWingman.Subscribe(delegate
		{
			objectMissileControlViewModel.GetState(objectBase, laser);
		});
	}

	private void GetState(Missile missile, SensorSystemLaserDesignator designator)
	{
		if (missile.GroupWingman.Value)
		{
			ObjectState = GroupWingman(missile);
		}
		else if (missile.ConnectionLost.Value && missile.ConnectionLostForever.Value)
		{
			ObjectState = LostConnection(missile);
		}
		else if (missile.ConnectionLost.Value && !missile.ConnectionLostForever.Value)
		{
			ObjectState = BadConnection(missile);
		}
		else
		{
			ObjectState = GoodConnection(missile);
		}
	}

	private string GroupWingman(Missile m)
	{
		string text = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_GroupWingman");
		text = text.Replace("${ParentName}", m._missileGroup._leader.getUIDAndName());
		string newValue = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_NoTarget");
		ObjectBase objectBase = null;
		if (m._missileGroup._leader.CurrentIntendedTargetObject != null)
		{
			objectBase = m._missileGroup._leader.CurrentIntendedTargetObject;
		}
		if (objectBase != null)
		{
			int mapContactID = objectBase.GetMapContactID();
			if (mapContactID > 0)
			{
				newValue = mapContactID.ToString();
				text = text + "<LineBreak/>" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_Target");
			}
			else
			{
				newValue = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_NoTargetInfo");
			}
		}
		return text.Replace("${TrackId}", newValue);
	}

	private string GoodConnection(Missile m)
	{
		string text = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_Datalink");
		if (m._seeker._guidanceType != AmmunitionParameters.GuidanceType.LaserHoming)
		{
			text = ((!(m._launchPlatform != null)) ? text.Replace("${ParentName}", "Nibiru") : text.Replace("${ParentName}", m._launchPlatform.getName()));
		}
		else
		{
			LaserSeeker laserSeeker = m._seeker as LaserSeeker;
			text = ((laserSeeker.GuidingDesignator.Value == null) ? text.Replace("${ParentName}", "Nibiru") : text.Replace("${ParentName}", laserSeeker.GuidingDesignator.Value._baseObject.getUIDAndName()));
		}
		string newValue = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_NoTarget");
		ObjectBase objectBase = null;
		if (m.CurrentStrongestTargetEcho != null)
		{
			objectBase = m.CurrentStrongestTargetEcho._objectBase;
		}
		else if (m.CurrentIntendedTargetObject != null)
		{
			objectBase = m.CurrentIntendedTargetObject;
		}
		if (objectBase != null)
		{
			int mapContactID = objectBase.GetMapContactID();
			if (mapContactID > 0)
			{
				newValue = mapContactID.ToString();
				text = text + "<LineBreak/>" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_Target");
			}
			else
			{
				newValue = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_NoTargetInfo");
			}
		}
		return text.Replace("${TrackId}", newValue);
	}

	private string BadConnection(Missile m)
	{
		string text = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_ConnectionLost");
		string newValue = ((m._launchPlatform != null) ? m._launchPlatform.Name.Value : "Other side");
		text = text.Replace("${ParentName}", newValue);
		text = ((m._seeker._guidanceType != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming) ? text.Replace("${Timer}", ": " + (10f - (Time.time - m._lostGuidanceTime)).ToString("F1")) : text.Replace("${Timer}", ""));
		return text + "<LineBreak/>" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_NoTargetInfo");
	}

	private string LostConnection(Missile m)
	{
		string text = Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_ConnectionLost");
		string newValue = ((m._launchPlatform != null) ? m._launchPlatform.Name.Value : "Other side");
		return text.Replace("${ParentName}", newValue).Replace("${Timer}", "") + "<LineBreak/>" + Singleton<LanguageResourceHandler>.Instance.getText("BottomRow", "Status_NoTargetInfo");
	}

	public void Dispose()
	{
	}
}
