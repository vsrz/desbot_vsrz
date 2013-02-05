using System;
using System.Collections;
using System.Windows.Forms;
namespace desBot
{
    /// <summary>
    /// Listview sorter: Alphabetical
    /// </summary>
    class AlphabeticalSorter : IComparer
    {
        int column;
        bool desc;

        public AlphabeticalSorter(int column, bool desc)
        {
            this.column = column;
            this.desc = desc;
        }

        public int Compare(object a, object b)
        {
            ListViewItem ia = a as ListViewItem;
            ListViewItem ib = b as ListViewItem;
            int result = string.Compare(ia.SubItems[column].Text, ib.SubItems[column].Text);
            return desc ? -result : result;
        }
    }

    /// <summary>
    /// Listview sorter: Numerical
    /// </summary>
    class NumericalSorter : IComparer
    {
        int column;
        bool desc;

        public NumericalSorter(int column, bool desc)
        {
            this.column = column;
            this.desc = desc;
        }

        public int Compare(object a, object b)
        {
            ListViewItem ia = a as ListViewItem;
            ListViewItem ib = b as ListViewItem;
            int da, db;
            bool va = int.TryParse(ia.SubItems[column].Text, out da);
            bool vb = int.TryParse(ib.SubItems[column].Text, out db);
            int result = 0;
            if (va)
            {
                if (vb) result = da - db;
                else result = -1;
            }
            else
            {
                if (vb) result = 1;
                else result = string.Compare(ia.SubItems[column].Text, ib.SubItems[column].Text);
            }
            return desc ? -result : result;
        }
    }

    /// <summary>
    /// Listview sorter: DateTime
    /// </summary>
    class DateTimeSorter : IComparer
    {
        int column;
        bool desc;

        public DateTimeSorter(int column, bool desc)
        {
            this.column = column;
            this.desc = desc;
        }

        public int Compare(object a, object b)
        {
            ListViewItem ia = a as ListViewItem;
            ListViewItem ib = b as ListViewItem;
            DateTime da, db;
            bool va = DateTime.TryParse(ia.SubItems[column].Text, out da);
            bool vb = DateTime.TryParse(ib.SubItems[column].Text, out db);
            int result = 0;
            if (va)
            {
                if (vb) result = DateTime.Compare(da, db);
                else result = -1;
            }
            else
            {
                if (vb) result = 1;
                else result = string.Compare(ia.SubItems[column].Text, ib.SubItems[column].Text);
            }
            return desc ? -result : result;
        }
    }
}
