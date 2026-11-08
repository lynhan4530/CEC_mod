using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

internal struct GuidanceNotice
{
	private ObjectBase fromA;

	private ObjectBase toA;

	private ObjectBase fromC;

	private ObjectBase toC;

	private float expires;

	internal string Current
	{
		get
		{
			if (Time.unscaledTime >= expires)
			{
				return null;
			}
			if ((Object)(object)fromA != (Object)null && (Object)(object)fromC != (Object)null)
			{
				return "A、C 均已接替";
			}
			if ((Object)(object)fromA != (Object)null)
			{
				return "A 接替  " + GuidanceStatus.Station(fromA) + " → " + GuidanceStatus.Station(toA);
			}
			if ((Object)(object)fromC != (Object)null)
			{
				return "C 接替  " + GuidanceStatus.Station(fromC) + " → " + GuidanceStatus.Station(toC);
			}
			return "链路已恢复";
		}
	}

	internal void Record(ObjectBase oldA, ObjectBase newA, ObjectBase oldC, ObjectBase newC, bool restored)
	{
		bool flag = (Object)(object)oldA != (Object)null && (Object)(object)oldA != (Object)(object)newA;
		bool flag2 = (Object)(object)oldC != (Object)null && (Object)(object)oldC != (Object)(object)newC;
		if (flag || flag2 || restored)
		{
			fromA = (flag ? oldA : null);
			toA = newA;
			fromC = (flag2 ? oldC : null);
			toC = newC;
			expires = Time.unscaledTime + 7f;
		}
	}
}
