using System;

namespace SanIsland.Merge
{
    public sealed class CurrencyService
    {
        readonly CurrencyState _state = new CurrencyState();

        public CurrencyState State => _state;

        public event Action Changed;
        public event Action<long> CoinsAdded;
        public event Action<long> CoinsSpent;

        public long GetCoins()
        {
            return _state.Coins;
        }

        public void AddCoins(long amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _state.Coins += amount;
            Changed?.Invoke();
            CoinsAdded?.Invoke(amount);
        }

        public bool CanSpendCoins(long amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            return _state.Coins >= amount;
        }

        public bool TrySpendCoins(long amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (_state.Coins < amount)
            {
                return false;
            }

            _state.Coins -= amount;
            Changed?.Invoke();
            CoinsSpent?.Invoke(amount);
            return true;
        }

        public void DebugSetCoins(long value)
        {
            _state.Coins = value < 0 ? 0 : value;
            Changed?.Invoke();
        }
    }
}
