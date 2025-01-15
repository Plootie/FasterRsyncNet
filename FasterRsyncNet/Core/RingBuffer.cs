using System.Collections;

namespace FasterRsyncNet.Core;

public class RingBuffer<T>(uint capacity) : IEnumerable<T>
{
    private const string EmptyBufferExceptionMessage = "Ring Buffer is empty.";
    private int _head = 0;
    private int _tail = 0;
    private int _size = 0;
    private readonly T[] _buffer = new T[capacity];

    public int Capacity => _buffer.Length;
    public int Count => _size;

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
        if(_size == 0)
            throw new InvalidOperationException(EmptyBufferExceptionMessage);
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
        T[] copy = new T[_size];
        CopyTo(copy);
        
        return copy;
    }
    
    public void CopyTo(T[] array, int arrayIndex = 0, int? count = null)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0 || arrayIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Index is out of bounds.");

        int elementsToCopy = count ?? _size;

        if (elementsToCopy < 0 || elementsToCopy > _size)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative and within the size of the buffer.");

        if (elementsToCopy > array.Length - arrayIndex)
            throw new ArgumentException("The target array does not have enough space to copy the elements.");

        CopyTo(array.AsSpan(arrayIndex, elementsToCopy));
    }
    
    public void CopyTo(Span<T> destination)
    {
        if (destination.Length < _size)
            throw new ArgumentException("Destination is too short.", nameof(destination));
        
        ReadOnlySpan<T> bufferAsSpan = _buffer.AsSpan();
        if (_head < _tail)
        {
            bufferAsSpan.Slice(_head, _size).CopyTo(destination);
        }
        else
        {
            int firstSegmentLength = _buffer.Length - _head;
            bufferAsSpan.Slice(_head, firstSegmentLength).CopyTo(destination);

            int secondSegmentLength = _size - firstSegmentLength;
            if (secondSegmentLength > 0)
            {
                bufferAsSpan.Slice(0, secondSegmentLength).CopyTo(destination[firstSegmentLength..]);
            }
        }
    }


    private void WrapCounter(ref int counter)
    {
        int tmp = counter + 1;
        
        tmp = (tmp >= _buffer.Length) ? 0 : tmp;
        
        counter = tmp;
    }
}