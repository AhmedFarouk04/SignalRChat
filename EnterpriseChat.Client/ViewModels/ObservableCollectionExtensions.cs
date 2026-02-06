// في نفس ChatViewModel.cs، أضف هذه الـ Extension Methods داخل namespace
using System.Collections.ObjectModel;

namespace EnterpriseChat.Client.ViewModels
{
    public static class ObservableCollectionExtensions
    {
        // ✅ AddRange لـ ObservableCollection
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (items == null) throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        // ✅ RemoveAll لـ ObservableCollection
        public static void RemoveAll<T>(this ObservableCollection<T> collection, Func<T, bool> predicate)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (predicate(collection[i]))
                {
                    collection.RemoveAt(i);
                }
            }
        }
    }
}