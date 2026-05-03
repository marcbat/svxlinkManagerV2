window.reflectorConfigEditor = window.reflectorConfigEditor || (function () {
    let editor = null;

    function normalizeNewLine(value) {
        return (value || "").replace(/\r\n/g, "\n");
    }

    return {
        init: function (textareaElement, initialValue, isReadOnly, dotNetRef) {
            if (!textareaElement || typeof CodeMirror === "undefined") {
                return false;
            }

            if (editor) {
                editor.toTextArea();
                editor = null;
            }

            textareaElement.value = normalizeNewLine(initialValue);

            editor = CodeMirror.fromTextArea(textareaElement, {
                mode: "properties",
                theme: "material-darker",
                lineNumbers: true,
                lineWrapping: false,
                readOnly: !!isReadOnly,
                tabSize: 4,
                indentWithTabs: false,
                viewportMargin: 12,
                extraKeys: {
                    Tab: function (cm) {
                        cm.replaceSelection("    ", "end");
                    }
                }
            });

            editor.on("change", function (cm) {
                const value = cm.getValue();
                textareaElement.value = value;
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync("OnConfigEditorChanged", value);
                }
            });

            editor.refresh();
            return true;
        },

        setReadOnly: function (isReadOnly) {
            if (!editor) {
                return;
            }

            editor.setOption("readOnly", !!isReadOnly);
        },

        setValue: function (value) {
            if (!editor) {
                return;
            }

            const normalizedValue = normalizeNewLine(value);
            if (editor.getValue() !== normalizedValue) {
                editor.setValue(normalizedValue);
            }
        },

        getValue: function () {
            if (!editor) {
                return "";
            }

            return editor.getValue();
        },

        refresh: function () {
            if (editor) {
                editor.refresh();
            }
        },

        dispose: function () {
            if (editor) {
                editor.toTextArea();
                editor = null;
            }
        }
    };
})();
