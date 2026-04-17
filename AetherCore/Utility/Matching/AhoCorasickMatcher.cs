namespace AetherCore.Utility
{
    public sealed class AhoCorasickMatcher
    {
        private sealed class Node
        {
            public Dictionary<char, Node> Next = new();
            public Node? Fail;
            public List<string> Outputs = new();
        }

        private readonly Node _root = new();

        public AhoCorasickMatcher(IEnumerable<string> keywords)
        {
            BuildTrie(keywords);
            BuildFailLinks();
        }

        private void BuildTrie(IEnumerable<string> keywords)
        {
            foreach (var kw in keywords)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;

                var node = _root;
                foreach (var ch in kw)
                {
                    if (!node.Next.TryGetValue(ch, out var next))
                    {
                        next            = new Node();
                        node.Next[ch]   = next;
                    }
                    node = next;
                }

                node.Outputs.Add(kw);
            }
        }

        private void BuildFailLinks()
        {
            var q = new Queue<Node>();

            // root 的子節點 fail 都指向 root
            foreach (var child in _root.Next.Values)
            {
                child.Fail = _root;
                q.Enqueue(child);
            }

            while (q.Count > 0)
            {
                var current = q.Dequeue();

                foreach (var (ch, next) in current.Next)
                {
                    q.Enqueue(next);

                    // 找 current 的 fail chain，看看有沒有 ch 的轉移
                    var f = current.Fail;
                    while (f != null && !f.Next.ContainsKey(ch))
                        f = f.Fail;

                    next.Fail = (f != null) ? f.Next[ch] : _root;

                    // fail 的 outputs 要繼承（關鍵）
                    if (next.Fail.Outputs.Count > 0)
                        next.Outputs.AddRange(next.Fail.Outputs);
                }
            }
        }

        /// <summary>
        /// 回傳命中的敏感詞（去重）
        /// </summary>
        public IReadOnlyCollection<string> FindMatches(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            var result  = new HashSet<string>();
            var node    = _root;

            foreach (var ch in text)
            {
                // 沒路就沿 fail 往回跳
                while (node != _root && !node.Next.ContainsKey(ch))
                    node = node.Fail ?? _root;

                // 有路就走，沒路就回 root
                if (node.Next.TryGetValue(ch, out var next))
                    node = next;
                else
                    node = _root;

                // 有 outputs 就代表命中
                if (node.Outputs.Count > 0)
                {
                    foreach (var kw in node.Outputs)
                        result.Add(kw);
                }
            }

            return result.Count == 0 ? Array.Empty<string>() : result;
        }

        public bool ContainsAny(string text) => FindMatches(text).Count > 0;
    }
}
