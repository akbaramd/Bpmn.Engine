import { html } from 'htm/preact';
import { TextFieldEntry, isTextFieldEntryEdited } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

export default function(element) {
    return [
        {
            id: 'formId',
            element,
            component: FormIdField,
            isEdited: isTextFieldEntryEdited
        }
    ];
}

function FormIdField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        return element.businessObject.formId || '';
    };

    const setValue = value => {
        return modeling.updateProperties(element, {
            formId: value
        });
    };

    return html`<${TextFieldEntry}
            id=${id}
            element=${element}
            description=${translate('Enter the form ID')}
            label=${translate('Form ID')}
            getValue=${getValue}
            setValue=${setValue}
            debounce=${debounce}
            tooltip=${translate('Enter the form ID associated with this task.')}
    />`;
}
