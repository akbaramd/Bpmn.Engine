import { html } from 'htm/preact';
import { TextFieldEntry, isTextFieldEntryEdited } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

export default function(element) {
    return [
        {
            id: 'inputNumber',
            element,
            component: NumberField,
            isEdited: isTextFieldEntryEdited
        }
    ];
}

function NumberField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        return element.businessObject.get('inputNumber || '';
    };

    const setValue = value => {
        return modeling.updateProperties(element, {
            inputNumber: value
        });
    };

    return html`<${TextFieldEntry}
        id=${id}
        element=${element}
        description=${translate('Enter a number')}
        label=${translate('Input Number')}
        getValue=${getValue}
        setValue=${setValue}
        debounce=${debounce}
        tooltip=${translate('Enter the number required for the task.')}
        inputType="number"
    />`;
}
