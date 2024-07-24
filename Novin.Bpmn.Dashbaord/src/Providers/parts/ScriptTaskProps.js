import { html } from 'htm/preact';
import { TextAreaEntry, isTextAreaEntryEdited } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

export default function(element) {
    return [
        {
            id: 'script',
            element,
            component: ScriptField,
            isEdited: isTextAreaEntryEdited
        }
    ];
}

function ScriptField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        return element.businessObject.get('script') || '';
    };

    const setValue = value => {
        return modeling.updateProperties(element, {
            script: value
        });
    };

    return html`<${TextAreaEntry}
        id=${id}
        element=${element}
        description=${translate('Enter the script')}
        label=${translate('Script')}
        getValue=${getValue}
        setValue=${setValue}
        debounce=${debounce}
        rows="15"
        tooltip=${translate('Enter the script to be executed.')}
    />`;
}
