(function ($, ko, moment) {

    //#region Utils

    var detectDataType = function (value) {
        for (var fname in detectDataType.typeDetection) {
            var f = detectDataType[fname];
            if (f(value)) {
                return detectDataType.typeDetection[fname];
            }
        }
        return null;
    }

    detectDataType.isString = function (value) {
        return typeof value === 'string';
    };
    detectDataType.isDate = function (value) {
        return typeof value === 'object' && Object.prototype.toString.call(value) === "[object Date]";
    };
    detectDataType.isMoment = function (value) {
        return moment.isMoment(value);
    };
    detectDataType.typeDetection = {
        "isMoment": "moment",
        "isString": "string",
        "isDate": "date"
    };
    
    var elBinder = function($el) {
        return {
            set: function (value) {
                var funcs = elBinder.functions[$el.data(elBinder.DATATYPE_KEY)] || elBinder.functions['_default'];
                var func = funcs['set'] || elBinder.functions['_default']['set'];
                return func($el, value);
            },
            get: function () {
                var funcs = elBinder.functions[$el.data(elBinder.DATATYPE_KEY)] || elBinder.functions['_default'];
                var func = funcs['get'] || elBinder.functions['_default']['get'];
                return func($el);
            },
            register: function (dataType) {
                $el.data(elBinder.DATATYPE_KEY, dataType);
            }                
        }
    }
    
    elBinder.DATATYPE_KEY = "datepicker.ko.dataType";

    elBinder.functions = {
        '_default': {
            get: function ($el) {
                return $el.datepicker('get');
            },
            set: function ($el, value) {
                $el.datepicker('set', value);
            }
        },
        'iso': {
            get: function ($el) {
                var value = $el.datepicker('get');
                return (value && value.format('YYYY-MM-DD'));
            },
            set: function ($el, value) {
                var mnt = moment(value);
                $el.datepicker('set', mnt);
            }
        },
        'format': {
            get: function ($el) {
                return $el.datepicker('getAsText');
            }
        },
        'date': {
            get: function ($el) {
                var value = $el.datepicker('get');
                return (value && value.toDate());
            }
        }
    };

    //#endregion

    ko.bindingHandlers.datepicker = {
        init: function (element, valueAccessor, allBindingsAccessor) {
            var options = allBindingsAccessor().datepickerOptions || {};
            var dataType = options.dataType || detectDataType(ko.utils.unwrapObservable(valueAccessor()));
            dataType = !dataType || dataType == 'string' ? 'iso' : dataType;
            var $el = $(element).datepicker(options);

            elBinder($el).register(dataType);

            ko.utils.registerEventHandler(element, "changeDate", function (event) {
                var accessor = valueAccessor();
                if (ko.isObservable(accessor)) {
                    var value = elBinder($el).get();
                    accessor(value);
                }
            });
        },
        update: function (element, valueAccessor) {
            elBinder($(element))
                .set(ko.utils.unwrapObservable(valueAccessor()));
        }
    };
})(jQuery, this.ko, this.moment);