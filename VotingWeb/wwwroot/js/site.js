var app = angular.module('VotingApp', ['ui.bootstrap']);
app.run(function () { });

app.controller('VotingAppController', ['$rootScope', '$scope', '$http', '$timeout', function ($rootScope, $scope, $http, $timeout) {

    $scope.refresh = function () {
        $http.get('api/Votes?c=' + new Date().getTime())
            .then(function (data, status) {
                $scope.votes = data;
                $scope.refreshVersion();
                $scope.refreshNodeName();
            }, function (data, status) {
                $scope.votes = undefined;
            });
    };

    $scope.remove = function (item) {
        $http.delete('api/Votes/' + item)
            .then(function (data, status) {
                $scope.refresh();
            })
    };

    $scope.add = function (item) {
        var fd = new FormData();
        fd.append('item', item);
        $http.put('api/Votes/' + item, fd, {
            transformRequest: angular.identity,
            headers: { 'Content-Type': undefined }
        })
            .then(function (data, status) {
                $scope.refresh();
                $scope.item = undefined;
            })
    };

    $scope.refreshVersion = function () {
        $http.get('api/Votes/appVersion')
            .then(function (data, status) {
                $scope.appVersion = data;
            }, function (data, status) {
                $scope.appVersion = "[unknown]";
            });
    };

    $scope.refreshNodeName = function () {
        $http.get('api/Votes/currentNode')
            .then(function (data, status) {
                $scope.currentNode = data;
            }, function (data, status) {
                $scope.currentNode = "[unknown]";
            });
    };

}]);