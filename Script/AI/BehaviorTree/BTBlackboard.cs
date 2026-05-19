using System.Collections.Generic;

public sealed class BTBlackboard
{
	private readonly Dictionary<string, object> _data = new();

	public void Set<T>(string key, T value)
	{
		_data[key] = value;
	}

	public bool TryGet<T>(string key, out T value)
	{
		if (_data.TryGetValue(key, out object raw) && raw is T typed)
		{
			value = typed;
			return true;
		}

		value = default;
		return false;
	}

	public T GetOrDefault<T>(string key, T fallback = default)
	{
		return TryGet<T>(key, out T value) ? value : fallback;
	}

	public T Get<T>(string key)
	{
		if (_data.TryGetValue(key, out var raw) && raw is T typed)
			return typed;
		return default;
	}
}
