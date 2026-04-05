using System;

namespace ImropveCrypto.Models;

public class Crypto
{
    private readonly bool[] _state; //  28 bits (индексы 0..27)
    private readonly bool[] _lfsr;  // LFSR state,
    private int _bitsEmittedFromInitial;   // сколько бит из initialState уже выдано
    private bool _initialEmitted;          // флаг, что всё начальное состояние выдано

    private readonly int _tap1 = 27;
    private readonly int _tap2 = 3;  
    
    public Crypto(byte[] initialState)
    {
        if (initialState == null || initialState.Length != 28)
            throw new ArgumentException("Initial state must be exactly 28 bytes (0 or 1).");

        _state = new bool[28];
        _lfsr = new bool[28];
        for (int i = 0; i < 28; i++)
        {
            _state[i] = initialState[i] == 1;
            _lfsr[i] = _state[i];
        }
        _bitsEmittedFromInitial = 0;
        _initialEmitted = false;
    }
    private bool Shift()
    {
        bool feedback = _lfsr[_tap1] ^ _lfsr[_tap2];
        for (int i = 27; i > 0; i--)
            _lfsr[i] = _lfsr[i - 1];
        _lfsr[0] = feedback;
        return feedback;
    }
    
    private bool NextBit()
    {
        if (!_initialEmitted)
        {
            if (_bitsEmittedFromInitial < 28)
            {
                bool bit = _state[_bitsEmittedFromInitial];
                _bitsEmittedFromInitial++;
                if (_bitsEmittedFromInitial == 28)
                    _initialEmitted = true;
                return bit;
            }
            else
            {
                _initialEmitted = true;
            }
        }
        return Shift();
    }
    
    public byte[] GenerateKeyBytes(int byteCount)
    {
        byte[] result = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            byte b = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                b = (byte)((b << 1) | (NextBit() ? 1 : 0));
            }
            result[i] = b;
        }
        return result;
    }
}