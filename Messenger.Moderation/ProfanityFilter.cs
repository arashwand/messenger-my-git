using System.Collections.Concurrent;
using System.Text;

namespace Messenger.Moderation
{
    // مدل نتیجه بررسی
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> FoundBadWords { get; set; } = new List<string>();
    }

    // نودهای درخت Trie
    internal class TrieNode
    {
        // استفاده از Dictionary برای جستجوی سریع فرزندان
        public Dictionary<char, TrieNode> Children { get; } = new Dictionary<char, TrieNode>();

        // آیا اینجا پایان یک کلمه بد است؟
        public bool IsEndOfWord { get; set; }

        // اصل کلمه بد (برای نمایش در گزارش)
        public string? Word { get; set; }
    }

    public class ProfanityFilter
    {
        private TrieNode _root;
        private readonly char _replacementChar = '*';

        public ProfanityFilter()
        {
            _root = new TrieNode();
        }

        // متد جدید برای بازسازی لیست بدون توقف
        public void Reload(IEnumerable<string> badWords)
        {
            // 1. ساخت یک درخت کاملاً جدید در حافظه موقت
            var newRoot = new TrieNode();

            foreach (var word in badWords)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;
                InsertIntoRoot(newRoot, word.Trim());
            }

            // 2. جایگزینی آنی (Atomic Swap)
            // در این لحظه تمام درخواست‌های جدید به درخت جدید هدایت می‌شوند
            _root = newRoot;
        }

        // --- فاز ۱: ساختن درخت (یک بار در شروع برنامه) ---
        public void Initialize(IEnumerable<string> badWords) => Reload(badWords);

        // متد Insert باید روی نود خاصی کار کند (تغییر جزئی نسبت به کد قبل)
        private void InsertIntoRoot(TrieNode rootNode, string word)
        {
            var current = rootNode;
            var normalizedWord = NormalizeCharForTrie(word); // متد قبلی که داشتید

            foreach (var c in normalizedWord)
            {
                if (!current.Children.TryGetValue(c, out var node))
                {
                    node = new TrieNode();
                    current.Children[c] = node;
                }
                current = node;
            }
            current.IsEndOfWord = true;
            current.Word = word;
        }


        // --- فاز ۲: جستجو (هزاران بار در ثانیه فراخوانی می‌شود) ---

        public ValidationResult ScanMessage(string message)
        {
            var result = new ValidationResult();
            if (string.IsNullOrWhiteSpace(message)) return result;

            // تبدیل پیام به آرایه کاراکتر برای سرعت بیشتر (جلوگیری از Substring)
            // اگر از .NET Core جدید استفاده می‌کنید ReadOnlySpan بهتر است اما برای سادگی اینجا آرایه استفاده شد
            char[] chars = message.ToCharArray();
            int length = chars.Length;

            for (int i = 0; i < length; i++)
            {
                // بررسی شروع تطابق از موقعیت i
                var foundWord = CheckStartingAt(chars, i);

                if (foundWord != null)
                {
                    result.IsValid = false;
                    // جلوگیری از تکراری شدن در لیست خروجی
                    if (!result.FoundBadWords.Contains(foundWord))
                    {
                        result.FoundBadWords.Add(foundWord);
                    }

                    // بهینه‌سازی: اگر می‌خواهید تمام کلمات را پیدا کنید، این خط را بردارید.
                    // اما برای سرعت بالا، معمولاً با اولین پیدا شدن می‌توان خارج شد مگر اینکه بخواهید سانسور کنید.
                    // i += foundWord.Length - 1; // پرش از روی کلمه پیدا شده (اختیاری)
                }
            }

            return result;
        }

        // متد سانسور کردن (جایگزینی با ستاره)
        public string CensorMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;
            char[] chars = message.ToCharArray();
            var bitMask = new bool[chars.Length]; // نشان می‌دهد کدام ایندکس‌ها باید سانسور شوند

            for (int i = 0; i < chars.Length; i++)
            {
                int matchLength = GetMatchLength(chars, i);
                if (matchLength > 0)
                {
                    // علامت‌گذاری بازه برای سانسور
                    for (int j = 0; j < matchLength; j++)
                    {
                        bitMask[i + j] = true;
                    }
                }
            }

            // ساخت رشته جدید
            var sb = new StringBuilder(chars.Length);
            for (int i = 0; i < chars.Length; i++)
            {
                sb.Append(bitMask[i] ? _replacementChar : chars[i]);
            }
            return sb.ToString();
        }

        // --- هسته مرکزی الگوریتم (Smart Walking) ---

        private string? CheckStartingAt(char[] text, int startIndex)
        {
            var current = _root;
            int textIndex = startIndex;

            while (textIndex < text.Length)
            {
                char rawChar = text[textIndex];

                // ۱. نرمال‌سازی کاراکتر جاری (مثلاً "ی" -> "ی")
                char normalizedChar = NormalizeSingleChar(rawChar);

                // ۲. منطق نادیده گرفتن نویز (Ignorable Characters)
                // اگر کاراکتر، حرف یا عدد نیست (مثل فاصله، نقطه، آندرلاین)، آن را رد کن
                // اما در درخت حرکت نکن (Stay on current node)
                if (!IsSignificantChar(normalizedChar))
                {
                    textIndex++;
                    continue;
                }

                // ۳. تلاش برای حرکت در درخت
                if (current.Children.TryGetValue(normalizedChar, out var nextNode))
                {
                    current = nextNode;
                    textIndex++;

                    // اگر به پایان یک کلمه بد رسیدیم
                    if (current.IsEndOfWord)
                    {
                        return current.Word;
                    }
                }
                else
                {
                    // مسیر قطع شد، تطابقی وجود ندارد
                    break;
                }
            }
            return null;
        }

        // متد کمکی برای سانسور (طول کلمه پیدا شده را در متن اصلی برمی‌گرداند)
        private int GetMatchLength(char[] text, int startIndex)
        {
            var current = _root;
            int textIndex = startIndex;
            int matchEndIndex = -1;

            while (textIndex < text.Length)
            {
                char rawChar = text[textIndex];
                char normalizedChar = NormalizeSingleChar(rawChar);

                if (!IsSignificantChar(normalizedChar))
                {
                    textIndex++;
                    continue;
                }

                if (current.Children.TryGetValue(normalizedChar, out var nextNode))
                {
                    current = nextNode;
                    textIndex++;
                    if (current.IsEndOfWord) matchEndIndex = textIndex;
                }
                else
                {
                    break;
                }
            }
            return (matchEndIndex != -1) ? (matchEndIndex - startIndex) : 0;
        }

        // --- توابع کمکی (Helper Functions) ---

        // کاراکترهای مهم: حروف و اعداد. بقیه (فاصله، .، -) نویز هستند.
        private bool IsSignificantChar(char c)
        {
            // برای فارسی و انگلیسی، حروف و اعداد را نگه می‌داریم
            // بازه حروف فارسی و عربی را می‌توان دقیق‌تر کرد ولی IsLetterOften کافی است
            return char.IsLetterOrDigit(c);
        }

        private string NormalizeCharForTrie(string word)
        {
            var sb = new StringBuilder();
            foreach (var c in word)
            {
                char norm = NormalizeSingleChar(c);
                if (IsSignificantChar(norm))
                    sb.Append(norm);
            }
            return sb.ToString();
        }

        private char NormalizeSingleChar(char c)
        {
            // تبدیل به حروف کوچک
            c = char.ToLowerInvariant(c);

            // مپینگ حروف فارسی/عربی
            return c switch
            {
                'ي' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                'آ' => 'ا',
                'أ' => 'ا',
                'إ' => 'ا',
                'ؤ' => 'و',
                '‌' => '\0', // نیم‌فاصله را کاملاً حذف یا نادیده می‌گیریم (اینجا با لاجیک بالا هندل می‌شود)
                _ => c
            };
        }
    }
}