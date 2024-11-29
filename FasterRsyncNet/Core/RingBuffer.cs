using System.Collections;

namespace FasterRsyncNet.Core;

public class RingBuffer<T> : IEnumerable<T>
{
    private const string EmptyBufferExceptionMessage = "Ring Buffer is empty.";
    private int _head = 0;
    private int _tail = 0;
    private int _size = 0;
    private readonly T[] _buffer;

    public int Capacity => _buffer.Length;
    public int Count => _size;

    public RingBuffer(uint capacity)
    {
        _buffer = new T[capacity];
    }
    
    public IEnumerator<T> GetEnumerator()
    {
        int index = _head;
        for(int i = 0; i < _size; i++, WrapCounter(ref index))
            yield return _buffer[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(T item)
    {
        _buffer[_tail] = item;
        WrapCounter(ref _tail);

        if (_size == _buffer.Length)
        {
            WrapCounter(ref _head);
        }
        else
        {
            _size++;
        }
    }

    public void Add(ReadOnlySpan<T> items)
    {
        if (items.Length > Capacity)
        {
            items = items.Slice(items.Length - Capacity);
        }

        int tailSpace = Capacity - _tail;
        int firstWriteCount = Math.Min(tailSpace, items.Length);
        int remainingWriteCount = items.Length - firstWriteCount;
        
        //First chunk copy
        items.Slice(0, firstWriteCount).CopyTo(_buffer.AsSpan(_tail));
        
        //Second chunk copy if needed
        if (remainingWriteCount > 0)
        {
            items.Slice(firstWriteCount, remainingWriteCount).CopyTo(_buffer.AsSpan(0, remainingWriteCount));
        }
        
        //Update head, tail and size
        _tail = (_tail + items.Length) % _buffer.Length;
        if (_size + items.Length > Capacity)
        {
            _head = _tail;
            _size = Capacity;
        }
        else
        {
            _size += items.Length;
        }
    }

    public T Take()
    {
        if(_size == 0)
            throw new InvalidOperationException(EmptyBufferExceptionMessage);
        T item = _buffer[_head];
        WrapCounter(ref _head);
        _size--;
        return item;
    }

    public IEnumerable<T> TakeMany(int count)
    {
        if(_size == 0)
            throw new InvalidOperationException(EmptyBufferExceptionMessage);
        for(int i = 0; i < count; i++)
            yield return Take();
    }

    public T Peek()
    {
        return _buffer[_head];
    }

    public void Clear()
    {
        if (_size == 0) return;

        if (_head < _tail)
        {
            Array.Clear(_buffer, _head, _size);
        }
        else
        {
            int firstPartLength = _buffer.Length - _head;
            Array.Clear(_buffer, _head, firstPartLength);
            Array.Clear(_buffer, 0, _tail);
        }
        
        _head = _tail = _size = 0;
    }

    //WrapCounter can't be used here due to not incrementing by 1
    public T this[int i]
    {
        get => _buffer[(_head + i) % _buffer.Length];
        set => _buffer[(_head + i) % _buffer.Length] = value;
    }

    public T[] ToArray()
    {
        if(_size == 0)
            return [];
        
        T[] copy = new T[_size];

        if (_head < _tail)
        {
            Array.Copy(_buffer, _head, copy, 0, _size);
        }
        else
        {
            int firstPartLength = _buffer.Length - _head;
            Array.Copy(_buffer, _head, copy, 0, firstPartLength);
            Array.Copy(_buffer, 0, copy, firstPartLength, _tail);
        }
        
        return copy;
    }

    private void WrapCounter(ref int counter)
    {
        int tmp = counter + 1;
        
        tmp = (tmp >= _buffer.Length) ? 0 : tmp;
        
        counter = tmp;
    }
}