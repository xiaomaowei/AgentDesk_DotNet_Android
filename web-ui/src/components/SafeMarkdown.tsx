import React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

interface SafeMarkdownProps {
  content: string;
  className?: string;
}

export const SafeMarkdown: React.FC<SafeMarkdownProps> = ({ content, className = '' }) => {
  return (
    <div className={`markdown-body ${className}`}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        skipHtml={true}
        components={{
          a: ({ node: _node, href, children, ...props }) => (
            <a
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(e) => {
                // Prevent navigation inside WebView
                e.preventDefault();
              }}
              {...props}
            >
              {children}
            </a>
          ),
          code: ({
            node: _node,
            inline,
            className: codeClassName,
            children,
            ...props
          }: {
            node?: unknown;
            inline?: boolean;
            className?: string;
            children?: React.ReactNode;
          }) => {
            if (inline) {
              return (
                <code className="inline-code" {...props}>
                  {children}
                </code>
              );
            }
            return (
              <pre className="code-block">
                <code className={codeClassName} {...props}>
                  {children}
                </code>
              </pre>
            );
          },
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
};
