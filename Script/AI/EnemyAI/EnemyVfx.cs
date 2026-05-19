using Godot;

public partial class EnemyBrain
{
    private void FlashHit()
    {
        if (_sprite == null) return;

        // Flash white then return to normal
        _sprite.Modulate = Colors.Red;

        var t = GetTree().CreateTimer(0.12f);
        t.Timeout += () =>
        {
            if (IsInstanceValid(_sprite))
                _sprite.Modulate = Colors.White;
        };
    }
}