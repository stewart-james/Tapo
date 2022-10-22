namespace Api;

public sealed record DeviceInfo
(
	string DeviceId,
	string FwVer,
	string HwVer,
	string Type,
	string Model,
	string Mac,
	string FwId,
	string OemId,
	string Ip,
	long TimeDiff,
	string Ssid,
	int Rssi,
	int SignalLevel,
	int Latitude,
	int Longitude,
	string Lang,
	string Avatar,
	string Region,
	string Specs,
	string Nickname,
	bool HasSetLocationInfo,
	bool DeviceOn,
	long OnTime,
	bool Overheated
);
