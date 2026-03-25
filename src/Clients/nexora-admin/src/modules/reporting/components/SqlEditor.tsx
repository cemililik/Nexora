import { useEffect, useRef, useCallback } from 'react';
import { EditorView, keymap, placeholder as cmPlaceholder } from '@codemirror/view';
import { EditorState } from '@codemirror/state';
import { sql, PostgreSQL } from '@codemirror/lang-sql';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { syntaxHighlighting, defaultHighlightStyle } from '@codemirror/language';
import { oneDark } from '@codemirror/theme-one-dark';
import { searchKeymap } from '@codemirror/search';

import { cn } from '@/shared/lib/utils';

interface SqlEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  darkMode?: boolean;
  readOnly?: boolean;
  'aria-label'?: string;
}

export function SqlEditor({ value, onChange, placeholder, className, darkMode, readOnly, 'aria-label': ariaLabel }: SqlEditorProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewRef = useRef<EditorView | null>(null);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  const createExtensions = useCallback(() => {
    const extensions = [
      sql({ dialect: PostgreSQL }),
      syntaxHighlighting(defaultHighlightStyle),
      history(),
      keymap.of([...defaultKeymap, ...historyKeymap, ...searchKeymap]),
      EditorView.lineWrapping,
      EditorView.updateListener.of((update) => {
        if (update.docChanged) {
          onChangeRef.current(update.state.doc.toString());
        }
      }),
    ];

    if (placeholder) {
      extensions.push(cmPlaceholder(placeholder));
    }

    if (darkMode) {
      extensions.push(oneDark);
    }

    if (readOnly) {
      extensions.push(EditorState.readOnly.of(true));
    }

    return extensions;
  }, [placeholder, darkMode, readOnly]);

  useEffect(() => {
    if (!containerRef.current) return;

    const state = EditorState.create({
      doc: value,
      extensions: createExtensions(),
    });

    const view = new EditorView({
      state,
      parent: containerRef.current,
    });

    viewRef.current = view;

    return () => {
      view.destroy();
      viewRef.current = null;
    };
    // Only create editor once on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Sync external value changes (e.g. form reset)
  useEffect(() => {
    const view = viewRef.current;
    if (!view) return;
    const current = view.state.doc.toString();
    if (current !== value) {
      view.dispatch({
        changes: { from: 0, to: current.length, insert: value },
      });
    }
  }, [value]);

  return (
    <div
      ref={containerRef}
      role="textbox"
      aria-label={ariaLabel}
      aria-multiline="true"
      className={cn(
        'overflow-hidden rounded-md border border-input bg-background text-sm [&_.cm-editor]:min-h-[100px] [&_.cm-editor]:outline-none [&_.cm-scroller]:p-2 [&_.cm-gutters]:bg-muted [&_.cm-activeLine]:bg-muted/50',
        className,
      )}
    />
  );
}
